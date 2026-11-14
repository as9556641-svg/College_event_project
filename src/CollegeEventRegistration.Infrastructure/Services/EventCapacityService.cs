using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Exceptions;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Thread-safe service for managing event seat reservation and cancellation.
/// Employs database transactions, RowVersion optimistic concurrency tokens,
/// and automated retry policies against concurrency conflicts.
/// </summary>
public class EventCapacityService : IEventCapacityService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EventCapacityService> _logger;

    public EventCapacityService(ApplicationDbContext context, ILogger<EventCapacityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EventRegistration> ReserveSeatAndRegisterAsync(
        int eventId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // Begin isolated database transaction
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // 1. Guard against duplicate active registration by the same student
                    var existingRegistration = await _context.EventRegistrations
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId && r.Status != RegistrationStatus.Cancelled, cancellationToken);

                    if (existingRegistration != null)
                    {
                        throw new InvalidOperationException($"User is already registered for this event (Registration #{existingRegistration.RegistrationNumber}).");
                    }

                    // 2. Fetch the target event with tracking to participate in concurrency checking
                    var ev = await _context.Events
                        .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

                    if (ev == null)
                    {
                        throw new KeyNotFoundException($"Event with ID {eventId} does not exist.");
                    }

                    if (ev.IsDeleted)
                    {
                        throw new InvalidOperationException("Cannot register for an event that has been deleted.");
                    }

                    if (ev.Status != EventStatus.Published && ev.Status != EventStatus.Approved)
                    {
                        throw new InvalidOperationException($"Event is not open for registration (Current status: {ev.Status}).");
                    }

                    // 3. Strict capacity threshold verification
                    if (ev.RegisteredCount >= ev.TotalCapacity)
                    {
                        _logger.LogWarning("Event {EventId} reached maximum capacity ({Count}/{Total}). Reservation rejected.",
                            eventId, ev.RegisteredCount, ev.TotalCapacity);
                        throw new EventCapacityExceededException(ev.Id, ev.TotalCapacity);
                    }

                    // 4. Increment RegisteredCount (Modifies Event entity, enforcing RowVersion concurrency token update on Save)
                    ev.RegisteredCount++;
                    ev.UpdatedAt = DateTime.UtcNow;

                    // 5. Construct new registration record
                    var registration = new EventRegistration
                    {
                        EventId = eventId,
                        UserId = userId,
                        RegistrationNumber = $"REG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                        RegisteredAt = DateTime.UtcNow,
                        Status = RegistrationStatus.Pending
                    };

                    await _context.EventRegistrations.AddAsync(registration, cancellationToken);

                    // 6. Save changes inside transaction — EF Core verifies RowVersion = original RowVersion
                    await _context.SaveChangesAsync(cancellationToken);

                    // 7. Commit atomic transaction
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation("Successfully reserved seat for User {UserId} in Event {EventId}. Seat {Count}/{Total}. Reg: {RegNumber}",
                        userId, eventId, ev.RegisteredCount, ev.TotalCapacity, registration.RegistrationNumber);

                    return registration;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency conflict on Event {EventId} seat reservation (Attempt {Attempt}/{MaxRetries}): {Message}",
                        eventId, attempt, maxRetries, ex.Message);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError("Exhausted concurrency retries for Event {EventId}. Throwing capacity exception.", eventId);
                        throw new EventCapacityExceededException(eventId, 0);
                    }

                    // Detach tracked entities and apply exponential backoff before retry
                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    // Check if SQL check constraint CK_Events_Capacity was triggered
                    if (ex.InnerException?.Message.Contains("CK_Events_Capacity") == true)
                    {
                        _logger.LogWarning("Database check constraint CK_Events_Capacity blocked registration for Event {EventId}.", eventId);
                        throw new EventCapacityExceededException(eventId, 0);
                    }

                    throw;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            throw new EventCapacityExceededException(eventId, 0);
        });
    }

    public async Task<bool> ReleaseSeatAsync(
        int registrationId,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    var registration = await _context.EventRegistrations
                        .Include(r => r.Event)
                        .FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);

                    if (registration == null)
                    {
                        throw new KeyNotFoundException($"Registration #{registrationId} does not exist.");
                    }

                    if (registration.Status == RegistrationStatus.Cancelled)
                    {
                        return false; // Already cancelled
                    }

                    registration.Status = RegistrationStatus.Cancelled;
                    registration.CancellationReason = cancellationReason;
                    registration.CancelledAt = DateTime.UtcNow;

                    // Decrement Event RegisteredCount
                    if (registration.Event != null && registration.Event.RegisteredCount > 0)
                    {
                        registration.Event.RegisteredCount--;
                        registration.Event.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation("Released seat for Registration #{RegId} (Event #{EventId}).",
                        registrationId, registration.EventId);

                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency conflict releasing seat for Registration #{RegId} (Attempt {Attempt}/{MaxRetries}): {Message}",
                        registrationId, attempt, maxRetries, ex.Message);

                    if (attempt == maxRetries)
                    {
                        throw;
                    }

                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            return false;
        });
    }
}

using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Exceptions;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Production service implementing the complete Phase 7 Event Registration Management lifecycle,
/// including eligibility verification, capacity reservation with optimistic concurrency,
/// duplicate registration prevention, student tenant isolation, coordinator scoping,
/// and administrative status governance.
/// </summary>
public class EventRegistrationService : IEventRegistrationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventDiscoveryService _discoveryService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<EventRegistrationService> _logger;
    private readonly INotificationService? _notificationService;

    public EventRegistrationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEventDiscoveryService discoveryService,
        IAuditLogService auditLogService,
        ILogger<EventRegistrationService> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _userManager = userManager;
        _discoveryService = discoveryService;
        _auditLogService = auditLogService;
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task<StudentRegistrationResultDto> RegisterStudentAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return new StudentRegistrationResultDto { Success = false, Message = "Invalid event identifier." };
        }

        if (string.IsNullOrWhiteSpace(studentUserId))
        {
            return new StudentRegistrationResultDto { Success = false, Message = "User authentication is required to register." };
        }

        // 1. Verify student user exists and is active
        var student = await _userManager.FindByIdAsync(studentUserId);
        if (student == null || !student.IsActive)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = "Student user account was not found or is currently inactive."
            };
        }

        // 2. Fetch event and verify Published status and non-deleted state
        var ev = await _context.Events
            .AsNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (ev == null || ev.IsDeleted)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = "The requested event could not be found or has been removed."
            };
        }

        // Strict Published-only validation for student registrations
        if (ev.Status != EventStatus.Published)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = $"Registrations are only accepted for Published events. Current event status is '{ev.Status}'."
            };
        }

        // 3. Validate registration window dates
        var now = DateTime.UtcNow;
        if (ev.RegistrationStartDate != default && now < ev.RegistrationStartDate)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = $"Registration for this event has not opened yet. It will open on {ev.RegistrationStartDate:MMM dd, yyyy HH:mm} UTC."
            };
        }

        if (ev.RegistrationEndDate != default && now > ev.RegistrationEndDate)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = $"The registration deadline for this event passed on {ev.RegistrationEndDate:MMM dd, yyyy HH:mm} UTC."
            };
        }

        // 4. Validate Department, Year, and College eligibility
        var eligibility = await _discoveryService.EvaluateStudentEligibilityAsync(eventId, studentUserId);
        if (!eligibility.IsEligible)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = $"Eligibility check failed: {eligibility.Message}"
            };
        }

        // 5. Pre-check for duplicate active registrations
        var existingReg = await _context.EventRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == studentUserId, cancellationToken);

        if (existingReg != null && existingReg.Status != RegistrationStatus.Cancelled)
        {
            return new StudentRegistrationResultDto
            {
                Success = false,
                RegistrationId = existingReg.Id,
                RegistrationNumber = existingReg.RegistrationNumber,
                Status = existingReg.Status,
                EventId = eventId,
                EventTitle = ev.Title,
                Message = $"You are already registered for this event (Registration #{existingReg.RegistrationNumber}, Status: {existingReg.Status})."
            };
        }

        // 6. Concurrency-safe atomic seat reservation and registration persistence
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Tracked event load with RowVersion concurrency token
                    var trackedEvent = await _context.Events
                        .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

                    if (trackedEvent == null || trackedEvent.IsDeleted || trackedEvent.Status != EventStatus.Published)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            Message = "The event is no longer available for registration."
                        };
                    }

                    // Strict capacity check
                    if (trackedEvent.RegisteredCount >= trackedEvent.TotalCapacity)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _logger.LogWarning("Event {EventId} reached capacity limit ({Count}/{Total}).",
                            eventId, trackedEvent.RegisteredCount, trackedEvent.TotalCapacity);
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            Message = $"Event has reached its maximum seat capacity of {trackedEvent.TotalCapacity} attendees."
                        };
                    }

                    // Check for active duplicate inside transaction
                    var trackedExisting = await _context.EventRegistrations
                        .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == studentUserId, cancellationToken);

                    if (trackedExisting != null && trackedExisting.Status != RegistrationStatus.Cancelled)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            RegistrationId = trackedExisting.Id,
                            RegistrationNumber = trackedExisting.RegistrationNumber,
                            Status = trackedExisting.Status,
                            EventId = eventId,
                            EventTitle = trackedEvent.Title,
                            Message = $"You already hold an active registration for this event (#{trackedExisting.RegistrationNumber})."
                        };
                    }

                    // Determine status: Pending if approval is explicitly required or if event is Paid, otherwise Confirmed
                    var isPaidEvent = trackedEvent.EventType == EventType.Paid && trackedEvent.RegistrationFee > 0;
                    var targetStatus = (trackedEvent.IsApprovalRequired || isPaidEvent)
                        ? RegistrationStatus.Pending
                        : RegistrationStatus.Confirmed;

                    // Increment seat count for Confirmed registrations
                    if (targetStatus == RegistrationStatus.Confirmed)
                    {
                        trackedEvent.RegisteredCount++;
                        trackedEvent.UpdatedAt = DateTime.UtcNow;
                    }

                    var regNumber = $"REG-{eventId:D4}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
                    EventRegistration finalRegistration;

                    if (trackedExisting != null)
                    {
                        // Reactivate previous cancelled record adhering to (EventId, UserId) unique composite index
                        trackedExisting.RegistrationNumber = regNumber;
                        trackedExisting.Status = targetStatus;
                        trackedExisting.RegisteredAt = DateTime.UtcNow;
                        trackedExisting.CancelledAt = null;
                        trackedExisting.CancellationReason = null;
                        trackedExisting.UpdatedAt = DateTime.UtcNow;
                        trackedExisting.IsActive = true;
                        finalRegistration = trackedExisting;
                    }
                    else
                    {
                        finalRegistration = new EventRegistration
                        {
                            EventId = eventId,
                            UserId = studentUserId,
                            RegistrationNumber = regNumber,
                            RegisteredAt = DateTime.UtcNow,
                            Status = targetStatus,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.EventRegistrations.AddAsync(finalRegistration, cancellationToken);
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    // Audit log the registration
                    await _auditLogService.LogAsync(
                        action: "StudentRegister",
                        entityName: "EventRegistration",
                        entityId: finalRegistration.Id.ToString(),
                        userId: studentUserId,
                        oldValues: null,
                        newValues: $"Status: {finalRegistration.Status}, RegNum: {finalRegistration.RegistrationNumber}, EventId: {eventId}"
                    );

                    _logger.LogInformation("Student {UserId} successfully registered for Event {EventId}. Reg: {RegNumber}, Status: {Status}",
                        studentUserId, eventId, finalRegistration.RegistrationNumber, finalRegistration.Status);

                    if (_notificationService != null && finalRegistration.Status == RegistrationStatus.Confirmed)
                    {
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                UserId = studentUserId,
                                EventId = eventId,
                                Title = "Registration Confirmed",
                                Message = $"You are confirmed for '{trackedEvent.Title}'. Registration #{finalRegistration.RegistrationNumber}.",
                                Type = NotificationType.RegistrationConfirmation,
                                ActionUrl = $"/Student/Registrations/Details/{finalRegistration.Id}",
                                CorrelationId = $"REG-CONFIRM-{finalRegistration.Id}"
                            }, cancellationToken);
                        }
                        catch (Exception nEx)
                        {
                            _logger.LogWarning(nEx, "Failed to dispatch registration confirmation notification for #{RegId}", finalRegistration.Id);
                        }
                    }

                    return new StudentRegistrationResultDto
                    {
                        Success = true,
                        RegistrationId = finalRegistration.Id,
                        RegistrationNumber = finalRegistration.RegistrationNumber,
                        Status = finalRegistration.Status,
                        EventId = eventId,
                        EventTitle = trackedEvent.Title,
                        Message = targetStatus == RegistrationStatus.Confirmed
                            ? $"Congratulations! You are officially registered for '{trackedEvent.Title}'. Registration #{finalRegistration.RegistrationNumber}."
                            : (isPaidEvent
                                ? $"Registration initiated for '{trackedEvent.Title}'. Please complete the fee payment of INR {trackedEvent.RegistrationFee:F2} to confirm your seat. Registration #{finalRegistration.RegistrationNumber}."
                                : $"Your registration request for '{trackedEvent.Title}' has been submitted for faculty review. Registration #{finalRegistration.RegistrationNumber}.")
                    };
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency token conflict registering student for Event {EventId} (Attempt {Attempt}/{MaxRetries}): {Message}",
                        eventId, attempt, maxRetries, ex.Message);

                    if (attempt == maxRetries)
                    {
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            Message = "High volume of simultaneous registrations. Please retry in a few moments."
                        };
                    }

                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (ex.InnerException?.Message.Contains("IX_EventRegistrations_EventId_UserId") == true)
                    {
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            Message = "A duplicate registration for this event was detected."
                        };
                    }
                    if (ex.InnerException?.Message.Contains("CK_Events_Capacity") == true)
                    {
                        return new StudentRegistrationResultDto
                        {
                            Success = false,
                            Message = "Event has reached maximum seat capacity."
                        };
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Unexpected error registering student {UserId} for event {EventId}", studentUserId, eventId);
                    return new StudentRegistrationResultDto
                    {
                        Success = false,
                        Message = "An error occurred while processing your registration. Please try again."
                    };
                }
            }

            return new StudentRegistrationResultDto
            {
                Success = false,
                Message = "Unable to complete registration due to server load. Please try again."
            };
        });
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> CancelStudentRegistrationAsync(
        int registrationId,
        string studentUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0)
        {
            return (false, "Invalid registration identifier.");
        }

        if (string.IsNullOrWhiteSpace(studentUserId))
        {
            return (false, "User authentication is required.");
        }

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
                        .Include(r => r.Ticket)
                        .FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);

                    if (registration == null)
                    {
                        return (false, "Registration record not found.");
                    }

                    // STRICT Student Tenant Isolation Guard
                    if (registration.UserId != studentUserId)
                    {
                        _logger.LogWarning("Security breach attempt: Student {UserId} attempted to cancel registration #{RegId} belonging to user {OwnerId}.",
                            studentUserId, registrationId, registration.UserId);
                        return (false, "You are not authorized to cancel this registration.");
                    }

                    if (registration.Status == RegistrationStatus.Cancelled)
                    {
                        return (false, "This registration is already cancelled.");
                    }

                    // Check if event has already begun
                    if (registration.Event != null && registration.Event.EventStartDate <= DateTime.UtcNow)
                    {
                        return (false, "Registration cannot be cancelled after the event has started.");
                    }

                    var oldStatus = registration.Status;

                    // Release seat if the registration was confirmed
                    if (oldStatus == RegistrationStatus.Confirmed && registration.Event != null && registration.Event.RegisteredCount > 0)
                    {
                        registration.Event.RegisteredCount--;
                        registration.Event.UpdatedAt = DateTime.UtcNow;
                    }

                    registration.Status = RegistrationStatus.Cancelled;
                    registration.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by student" : reason.Trim();
                    registration.CancelledAt = DateTime.UtcNow;
                    registration.UpdatedAt = DateTime.UtcNow;

                    if (registration.Ticket != null)
                    {
                        registration.Ticket.IsActive = false;
                        registration.Ticket.UpdatedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        action: "CancelRegistration",
                        entityName: "EventRegistration",
                        entityId: registration.Id.ToString(),
                        userId: studentUserId,
                        oldValues: $"Status: {oldStatus}",
                        newValues: $"Status: Cancelled, Reason: {registration.CancellationReason}"
                    );

                    _logger.LogInformation("Student {UserId} cancelled registration #{RegNum} (ID: {RegId}). Seat released.",
                        studentUserId, registration.RegistrationNumber, registration.Id);

                    if (_notificationService != null)
                    {
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                UserId = studentUserId,
                                EventId = registration.EventId,
                                Title = "Registration Cancelled",
                                Message = $"Your registration for '{registration.Event?.Title}' has been cancelled.",
                                Type = NotificationType.RegistrationCancelled,
                                ActionUrl = "/Student/Registrations",
                                CorrelationId = $"REG-CANCEL-{registration.Id}"
                            }, cancellationToken);
                        }
                        catch (Exception nEx)
                        {
                            _logger.LogWarning(nEx, "Failed to dispatch cancellation notification for #{RegId}", registration.Id);
                        }
                    }

                    return (true, $"Your registration #{registration.RegistrationNumber} has been successfully cancelled.");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency conflict cancelling registration #{RegId} (Attempt {Attempt}/{MaxRetries}): {Message}",
                        registrationId, attempt, maxRetries, ex.Message);

                    if (attempt == maxRetries)
                    {
                        return (false, "System was busy. Please try cancelling again.");
                    }

                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Error cancelling registration #{RegId}", registrationId);
                    return (false, "An error occurred while cancelling your registration.");
                }
            }

            return (false, "Cancellation timed out. Please try again.");
        });
    }

    /// <inheritdoc />
    public async Task<PagedResult<RegistrationSummaryDto>> GetStudentRegistrationsAsync(
        string studentUserId,
        StudentRegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new StudentRegistrationFilterCriteria();

        // STRICT student tenant scoping: r.UserId == studentUserId
        var query = _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.Ticket)
            .Where(r => r.UserId == studentUserId && !r.Event.IsDeleted);

        if (criteria.Status.HasValue)
        {
            query = query.Where(r => r.Status == criteria.Status.Value);
        }

        if (criteria.OnlyUpcoming)
        {
            query = query.Where(r => r.Event.EventStartDate >= DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.RegistrationNumber.ToLower().Contains(term) ||
                r.Event.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 50);

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistrationSummaryDto
            {
                Id = r.Id,
                RegistrationNumber = r.RegistrationNumber,
                EventId = r.EventId,
                EventTitle = r.Event.Title,
                EventDepartmentCode = r.Event.Department.Code,
                EventStartDate = r.Event.EventStartDate,
                EventEndDate = r.Event.EventEndDate,
                EventMode = r.Event.EventMode,
                EventType = r.Event.EventType,
                RegistrationFee = r.Event.RegistrationFee,
                UserId = r.UserId,
                StudentName = r.User.FullName,
                StudentEmail = r.User.Email ?? string.Empty,
                StudentRollNumber = r.User.RollNumber,
                StudentDepartment = r.User.Department,
                StudentYearOfStudy = r.User.YearOfStudy,
                StudentCollegeName = r.User.CollegeName,
                RegisteredAt = r.RegisteredAt,
                Status = r.Status,
                CancellationReason = r.CancellationReason,
                CancelledAt = r.CancelledAt,
                TicketCode = r.Ticket != null ? r.Ticket.TicketCode : null
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RegistrationSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<RegistrationDetailDto?> GetRegistrationDetailsForStudentAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        var reg = await _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(r => r.User)
            .Include(r => r.Ticket)
            .Include(r => r.Attendance)
            .FirstOrDefaultAsync(r => r.Id == registrationId && !r.Event.IsDeleted, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        // STRICT Student Tenant Isolation
        if (reg.UserId != studentUserId)
        {
            _logger.LogWarning("Tenant violation attempt: User {UserId} tried reading registration #{RegId} of {OwnerId}",
                studentUserId, registrationId, reg.UserId);
            return null;
        }

        return MapToDetailDto(reg);
    }

    /// <inheritdoc />
    public async Task<RegistrationDetailDto?> GetRegistrationDetailsForStaffAsync(
        int registrationId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        var reg = await _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(r => r.User)
            .Include(r => r.Ticket)
            .Include(r => r.Attendance)
            .FirstOrDefaultAsync(r => r.Id == registrationId && !r.Event.IsDeleted, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        // Coordinator Scoping Guard
        if (staffRole == nameof(UserRole.Coordinator))
        {
            var isCoordinating = await _context.Events
                .AnyAsync(e => e.Id == reg.EventId && !e.IsDeleted && (e.CreatedByUserId == staffUserId || e.Coordinators.Any(c => c.UserId == staffUserId)), cancellationToken);

            if (!isCoordinating)
            {
                _logger.LogWarning("Coordinator {UserId} denied access to registration #{RegId} of unassigned Event #{EventId}",
                    staffUserId, registrationId, reg.EventId);
                return null;
            }
        }

        return MapToDetailDto(reg);
    }

    /// <inheritdoc />
    public async Task<PagedResult<RegistrationSummaryDto>> GetRegistrationsForAdminAsync(
        RegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new RegistrationFilterCriteria();

        var query = _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.User)
            .Include(r => r.Ticket)
            .Where(r => !r.Event.IsDeleted);

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(r => r.EventId == criteria.EventId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(r => r.Status == criteria.Status.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(r => r.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.RegistrationNumber.ToLower().Contains(term) ||
                r.User.FirstName.ToLower().Contains(term) ||
                r.User.LastName.ToLower().Contains(term) ||
                (r.User.Email != null && r.User.Email.ToLower().Contains(term)) ||
                (r.User.RollNumber != null && r.User.RollNumber.ToLower().Contains(term)) ||
                r.Event.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistrationSummaryDto
            {
                Id = r.Id,
                RegistrationNumber = r.RegistrationNumber,
                EventId = r.EventId,
                EventTitle = r.Event.Title,
                EventDepartmentCode = r.Event.Department.Code,
                EventStartDate = r.Event.EventStartDate,
                EventEndDate = r.Event.EventEndDate,
                EventMode = r.Event.EventMode,
                EventType = r.Event.EventType,
                RegistrationFee = r.Event.RegistrationFee,
                UserId = r.UserId,
                StudentName = r.User.FullName,
                StudentEmail = r.User.Email ?? string.Empty,
                StudentRollNumber = r.User.RollNumber,
                StudentDepartment = r.User.Department,
                StudentYearOfStudy = r.User.YearOfStudy,
                StudentCollegeName = r.User.CollegeName,
                RegisteredAt = r.RegisteredAt,
                Status = r.Status,
                CancellationReason = r.CancellationReason,
                CancelledAt = r.CancelledAt,
                TicketCode = r.Ticket != null ? r.Ticket.TicketCode : null
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RegistrationSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<RegistrationSummaryDto>> GetRegistrationsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        RegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return new PagedResult<RegistrationSummaryDto> { Items = new List<RegistrationSummaryDto>() };
        }

        // STRICT Coordinator Scope Validation
        var isAssigned = await _context.Events
            .AnyAsync(e => e.Id == eventId && !e.IsDeleted && (e.CreatedByUserId == coordinatorUserId || e.Coordinators.Any(c => c.UserId == coordinatorUserId)), cancellationToken);

        if (!isAssigned)
        {
            _logger.LogWarning("Coordinator {UserId} unauthorized access attempt to registrations for unassigned Event {EventId}",
                coordinatorUserId, eventId);
            throw new UnauthorizedAccessException($"Coordinator is not assigned to Event #{eventId}.");
        }

        criteria ??= new RegistrationFilterCriteria();
        criteria.EventId = eventId;

        var query = _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.User)
            .Include(r => r.Ticket)
            .Where(r => r.EventId == eventId && !r.Event.IsDeleted);

        if (criteria.Status.HasValue)
        {
            query = query.Where(r => r.Status == criteria.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.RegistrationNumber.ToLower().Contains(term) ||
                r.User.FirstName.ToLower().Contains(term) ||
                r.User.LastName.ToLower().Contains(term) ||
                (r.User.Email != null && r.User.Email.ToLower().Contains(term)) ||
                (r.User.RollNumber != null && r.User.RollNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistrationSummaryDto
            {
                Id = r.Id,
                RegistrationNumber = r.RegistrationNumber,
                EventId = r.EventId,
                EventTitle = r.Event.Title,
                EventDepartmentCode = r.Event.Department.Code,
                EventStartDate = r.Event.EventStartDate,
                EventEndDate = r.Event.EventEndDate,
                EventMode = r.Event.EventMode,
                EventType = r.Event.EventType,
                RegistrationFee = r.Event.RegistrationFee,
                UserId = r.UserId,
                StudentName = r.User.FullName,
                StudentEmail = r.User.Email ?? string.Empty,
                StudentRollNumber = r.User.RollNumber,
                StudentDepartment = r.User.Department,
                StudentYearOfStudy = r.User.YearOfStudy,
                StudentCollegeName = r.User.CollegeName,
                RegisteredAt = r.RegisteredAt,
                Status = r.Status,
                CancellationReason = r.CancellationReason,
                CancelledAt = r.CancelledAt,
                TicketCode = r.Ticket != null ? r.Ticket.TicketCode : null
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RegistrationSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> UpdateRegistrationStatusAsync(
        int registrationId,
        RegistrationStatus newStatus,
        string updaterUserId,
        string updaterRole,
        string? remarks = null,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0)
        {
            return (false, "Invalid registration identifier.");
        }

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
                        .Include(r => r.Ticket)
                        .FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);

                    if (registration == null || registration.Event == null || registration.Event.IsDeleted)
                    {
                        return (false, "Registration or associated event not found.");
                    }

                    // Strict coordinator scoping check
                    if (updaterRole == nameof(UserRole.Coordinator))
                    {
                        var isCoordinating = await _context.Events
                            .AnyAsync(e => e.Id == registration.EventId && !e.IsDeleted && (e.CreatedByUserId == updaterUserId || e.Coordinators.Any(c => c.UserId == updaterUserId)), cancellationToken);

                        if (!isCoordinating)
                        {
                            return (false, "Coordinators may only update registrations for their assigned events.");
                        }
                    }

                    var currentStatus = registration.Status;

                    if (currentStatus == newStatus)
                    {
                        return (true, $"Registration is already in '{newStatus}' status.");
                    }

                    // Enforce Valid State Machine Transitions
                    var isValidTransition = (currentStatus, newStatus) switch
                    {
                        (RegistrationStatus.Pending, RegistrationStatus.Confirmed) => true,
                        (RegistrationStatus.Pending, RegistrationStatus.Waitlisted) => true,
                        (RegistrationStatus.Pending, RegistrationStatus.Cancelled) => true,

                        (RegistrationStatus.Confirmed, RegistrationStatus.Cancelled) => true,
                        (RegistrationStatus.Confirmed, RegistrationStatus.Waitlisted) => true,

                        (RegistrationStatus.Waitlisted, RegistrationStatus.Confirmed) => true,
                        (RegistrationStatus.Waitlisted, RegistrationStatus.Cancelled) => true,

                        // SuperAdmin/Admin reactivation from Cancelled
                        (RegistrationStatus.Cancelled, RegistrationStatus.Confirmed) when updaterRole == nameof(UserRole.SuperAdmin) || updaterRole == nameof(UserRole.Admin) => true,
                        (RegistrationStatus.Cancelled, RegistrationStatus.Pending) when updaterRole == nameof(UserRole.SuperAdmin) || updaterRole == nameof(UserRole.Admin) => true,

                        _ => false
                    };

                    if (!isValidTransition)
                    {
                        return (false, $"Invalid state transition: Cannot transition registration from '{currentStatus}' to '{newStatus}'.");
                    }

                    // Capacity reservation adjustments
                    if (newStatus == RegistrationStatus.Confirmed && currentStatus != RegistrationStatus.Confirmed)
                    {
                        if (registration.Event.RegisteredCount >= registration.Event.TotalCapacity)
                        {
                            return (false, $"Cannot confirm registration: Event has reached full capacity ({registration.Event.TotalCapacity} seats).");
                        }
                        registration.Event.RegisteredCount++;
                        registration.Event.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (currentStatus == RegistrationStatus.Confirmed && newStatus != RegistrationStatus.Confirmed)
                    {
                        if (registration.Event.RegisteredCount > 0)
                        {
                            registration.Event.RegisteredCount--;
                            registration.Event.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    registration.Status = newStatus;
                    registration.UpdatedAt = DateTime.UtcNow;

                    if (newStatus == RegistrationStatus.Cancelled)
                    {
                        registration.CancelledAt = DateTime.UtcNow;
                        registration.CancellationReason = string.IsNullOrWhiteSpace(remarks)
                            ? $"Cancelled by {updaterRole}"
                            : remarks.Trim();

                        if (registration.Ticket != null)
                        {
                            registration.Ticket.IsActive = false;
                            registration.Ticket.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else if (currentStatus == RegistrationStatus.Cancelled)
                    {
                        registration.CancelledAt = null;
                        registration.CancellationReason = null;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    await _auditLogService.LogAsync(
                        action: "UpdateRegistrationStatus",
                        entityName: "EventRegistration",
                        entityId: registration.Id.ToString(),
                        userId: updaterUserId,
                        oldValues: $"Status: {currentStatus}",
                        newValues: $"Status: {newStatus}, Remarks: {remarks}"
                    );

                    _logger.LogInformation("Registration #{RegNum} status changed {Old} -> {New} by {UserId} ({Role})",
                        registration.RegistrationNumber, currentStatus, newStatus, updaterUserId, updaterRole);

                    if (_notificationService != null && currentStatus == RegistrationStatus.Waitlisted && newStatus == RegistrationStatus.Confirmed)
                    {
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                UserId = registration.UserId,
                                EventId = registration.EventId,
                                Title = "Promoted from Waitlist",
                                Message = $"Great news! You have been confirmed for '{registration.Event?.Title}'. Registration #{registration.RegistrationNumber}.",
                                Type = NotificationType.WaitlistPromoted,
                                ActionUrl = $"/Student/Registrations/Details/{registration.Id}",
                                CorrelationId = $"REG-PROMOTE-{registration.Id}"
                            }, cancellationToken);
                        }
                        catch (Exception nEx)
                        {
                            _logger.LogWarning(nEx, "Failed to dispatch waitlist promotion notification for #{RegId}", registration.Id);
                        }
                    }

                    return (true, $"Registration #{registration.RegistrationNumber} status updated to '{newStatus}' successfully.");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Concurrency conflict updating status for registration #{RegId}: {Message}", registrationId, ex.Message);
                    if (attempt == maxRetries)
                    {
                        return (false, "Concurrency conflict during status update. Please try again.");
                    }
                    _context.ChangeTracker.Clear();
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Error updating registration #{RegId} status", registrationId);
                    return (false, "An unexpected error occurred while updating registration status.");
                }
            }

            return (false, "Operation timed out.");
        });
    }

    /// <inheritdoc />
    public async Task<EventRegistration?> GetActiveStudentRegistrationForEventAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EventRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.EventId == eventId &&
                r.UserId == studentUserId &&
                r.Status != RegistrationStatus.Cancelled,
                cancellationToken);
    }

    private static RegistrationDetailDto MapToDetailDto(EventRegistration reg)
    {
        return new RegistrationDetailDto
        {
            Id = reg.Id,
            RegistrationNumber = reg.RegistrationNumber,
            RegisteredAt = reg.RegisteredAt,
            Status = reg.Status,
            CancellationReason = reg.CancellationReason,
            CancelledAt = reg.CancelledAt,
            EventId = reg.EventId,
            EventTitle = reg.Event.Title,
            EventDescription = reg.Event.Description,
            BannerImageUrl = reg.Event.BannerImageUrl,
            DepartmentName = reg.Event.Department?.Name ?? string.Empty,
            CategoryName = reg.Event.EventCategory?.Name ?? string.Empty,
            EventMode = reg.Event.EventMode,
            Venue = reg.Event.Venue,
            OnlineMeetingUrl = reg.Event.OnlineMeetingUrl,
            EventStartDate = reg.Event.EventStartDate,
            EventEndDate = reg.Event.EventEndDate,
            EventType = reg.Event.EventType,
            RegistrationFee = reg.Event.RegistrationFee,
            StudentId = reg.UserId,
            StudentName = reg.User?.FullName ?? string.Empty,
            StudentEmail = reg.User?.Email ?? string.Empty,
            StudentRollNumber = reg.User?.RollNumber,
            StudentDepartment = reg.User?.Department,
            StudentYearOfStudy = reg.User?.YearOfStudy,
            StudentCollegeName = reg.User?.CollegeName,
            TicketCode = reg.Ticket?.TicketCode,
            IsAttended = reg.Attendance != null && reg.Attendance.Status == AttendanceStatus.CheckedIn
        };
    }
}

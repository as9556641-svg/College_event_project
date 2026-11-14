using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service interface for thread-safe event seat reservation and cancellation
/// utilizing database transactions, RowVersion optimistic concurrency, and capacity limit checks.
/// </summary>
public interface IEventCapacityService
{
    /// <summary>
    /// Atomically reserves a seat for an event within a database transaction,
    /// verifying capacity, incrementing RegisteredCount, creating the registration,
    /// and handling DbUpdateConcurrencyException with optimistic concurrency.
    /// </summary>
    Task<EventRegistration> ReserveSeatAndRegisterAsync(
        int eventId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically releases a reserved seat upon cancellation within a database transaction,
    /// decrementing RegisteredCount, updating registration status, and utilizing optimistic concurrency.
    /// </summary>
    Task<bool> ReleaseSeatAsync(
        int registrationId,
        string cancellationReason,
        CancellationToken cancellationToken = default);
}

using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service managing event admission tickets, eligibility verification,
/// duplicate prevention, lifecycle invalidation, and role-scoped queries.
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Retrieves an admission ticket for an eligible registration, enforcing strict student tenant isolation.
    /// If the registration is confirmed and eligible, but the ticket has not yet been generated, it will be generated and persisted.
    /// </summary>
    Task<TicketDto?> GetOrGenerateTicketForStudentAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a ticket by its unique ticket code for the owning student.
    /// </summary>
    Task<TicketDto?> GetTicketByCodeForStudentAsync(
        string ticketCode,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all valid and active admission tickets for a student.
    /// </summary>
    Task<PagedResult<TicketDto>> GetStudentTicketsAsync(
        string studentUserId,
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a ticket for an eligible registration.
    /// Strictly rejects Pending, Waitlisted, Cancelled, and unpaid paid-event registrations.
    /// </summary>
    Task<TicketDto> GenerateTicketAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cryptographically and database-verifies a scanned QR payload or ticket code string.
    /// </summary>
    Task<TicketValidationResultDto> ValidateTicketPayloadAsync(
        string qrPayload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Database-verifies a ticket by its reference code.
    /// </summary>
    Task<TicketValidationResultDto> ValidateTicketCodeAsync(
        string ticketCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates an active ticket when a registration is cancelled or payment reversed.
    /// </summary>
    Task<bool> InvalidateTicketAsync(
        int registrationId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Master ticket ledger for campus administrators.
    /// </summary>
    Task<PagedResult<TicketDto>> GetTicketsForAdminAsync(
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scoped ticket ledger for event coordinators restricted to their assigned/created events.
    /// </summary>
    Task<PagedResult<TicketDto>> GetTicketsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detailed inspection view for staff (Admin or assigned Coordinator).
    /// </summary>
    Task<TicketDto?> GetTicketDetailsForStaffAsync(
        int ticketId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);
}

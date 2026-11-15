using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service interface for managing event registration lifecycle, student enrollments,
/// state machine transitions, strict tenant and coordinator scoping, and administrative governance.
/// </summary>
public interface IEventRegistrationService
{
    /// <summary>
    /// Validates student eligibility, event published status, registration window, capacity,
    /// and duplicate rules, then atomically reserves a seat and creates a registration record.
    /// </summary>
    Task<StudentRegistrationResultDto> RegisterStudentAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a student's active registration, releases the event seat reservation,
    /// and records cancellation reason and timestamp.
    /// Strictly verifies that the registration belongs to the requesting student.
    /// </summary>
    Task<(bool Success, string Message)> CancelStudentRegistrationAsync(
        int registrationId,
        string studentUserId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of registrations for an authenticated student with strict tenant isolation.
    /// </summary>
    Task<PagedResult<RegistrationSummaryDto>> GetStudentRegistrationsAsync(
        string studentUserId,
        StudentRegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed registration information for a student, strictly verifying student ownership.
    /// </summary>
    Task<RegistrationDetailDto?> GetRegistrationDetailsForStudentAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed registration information for administrative or coordinator inspection,
    /// enforcing coordinator event assignment scoping.
    /// </summary>
    Task<RegistrationDetailDto?> GetRegistrationDetailsForStaffAsync(
        int registrationId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, searchable, and filterable list of registrations across events for SuperAdmin/Admin.
    /// </summary>
    Task<PagedResult<RegistrationSummaryDto>> GetRegistrationsForAdminAsync(
        RegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of registrations for an event assigned to a specific coordinator,
    /// strictly enforcing coordinator authorization.
    /// </summary>
    Task<PagedResult<RegistrationSummaryDto>> GetRegistrationsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        RegistrationFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an administrative or coordinator status transition (Pending -> Confirmed, Waitlisted, Cancelled)
    /// while validating state machine rules, capacity constraints, and recording audit entries.
    /// </summary>
    Task<(bool Success, string Message)> UpdateRegistrationStatusAsync(
        int registrationId,
        RegistrationStatus newStatus,
        string updaterUserId,
        string updaterRole,
        string? remarks = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active registration (Pending, Confirmed, or Waitlisted) for a given student and event, if any.
    /// </summary>
    Task<EventRegistration?> GetActiveStudentRegistrationForEventAsync(
        int eventId,
        string studentUserId,
        CancellationToken cancellationToken = default);
}

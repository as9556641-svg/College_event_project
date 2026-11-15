using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service interface for Phase 11: Feedback & Ratings.
/// Enforces post-event submission eligibility (confirmed registration + checked-in attendance + ended event),
/// rating boundaries, comment sanitization, coordinator event-level scoping, and aggregated analytics.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Evaluates eligibility and saves a student's post-event evaluation.
    /// Handles duplicate submissions, concurrency races, rating validation, and audit logging.
    /// </summary>
    Task<FeedbackResultDto> SubmitFeedbackAsync(
        FeedbackSubmissionDto dto,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an authenticated student is eligible to submit feedback for a specific registration.
    /// </summary>
    Task<StudentFeedbackEligibilityDto> CheckStudentEligibilityAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all completed and attended registrations for a student, indicating feedback eligibility status.
    /// </summary>
    Task<IReadOnlyList<StudentFeedbackEligibilityDto>> GetStudentFeedbackEligibleEventsAsync(
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves feedback details for a specific registration, enforcing tenant isolation or staff authorization.
    /// </summary>
    Task<FeedbackItemDto?> GetFeedbackByRegistrationIdAsync(
        int registrationId,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves feedback details by ID, enforcing tenant isolation or staff authorization.
    /// </summary>
    Task<FeedbackItemDto?> GetFeedbackByIdAsync(
        int feedbackId,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregated rating metrics and recent feedback items for an event.
    /// Enforces coordinator event-scoping and admin cross-campus authorization.
    /// </summary>
    Task<EventFeedbackSummaryDto> GetEventFeedbackSummaryAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of feedback records matching filter criteria.
    /// Enforces coordinator event scoping (only assigned events) or full admin cross-campus visibility.
    /// </summary>
    Task<PagedResult<FeedbackItemDto>> GetFeedbackRecordsAsync(
        FeedbackFilterCriteria criteria,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a staff member (Coordinator or Admin/SuperAdmin) is authorized to access an event's feedback.
    /// </summary>
    Task<bool> CanStaffAccessEventFeedbackAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);
}

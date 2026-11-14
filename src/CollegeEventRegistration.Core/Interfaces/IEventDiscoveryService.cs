using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for student-facing event discovery, searching, filtering, and eligibility assessment.
/// Strictly enforces server-side Published-only visibility rules.
/// </summary>
public interface IEventDiscoveryService
{
    /// <summary>
    /// Retrieves a paginated list of published events matching the query criteria.
    /// Strictly filters e.Status == EventStatus.Published and !e.IsDeleted.
    /// </summary>
    Task<PagedResult<Event>> GetPublishedEventsAsync(StudentEventQueryCriteria criteria);

    /// <summary>
    /// Retrieves the comprehensive event details for a student.
    /// Returns null if the event does not exist, is soft-deleted, or is NOT Published.
    /// Never exposes internal administrative audit data or rejection reasons.
    /// </summary>
    Task<Event?> GetPublishedEventDetailsAsync(int eventId);

    /// <summary>
    /// Retrieves active event categories with the live count of active published events.
    /// </summary>
    Task<IReadOnlyList<CategoryWithCountDto>> GetActiveCategoriesWithCountAsync();

    /// <summary>
    /// Retrieves active departments for catalog filtering.
    /// </summary>
    Task<IReadOnlyList<Department>> GetActiveDepartmentsAsync();

    /// <summary>
    /// Evaluates whether an authenticated student satisfies the event's configured eligibility rules.
    /// </summary>
    Task<StudentEventEligibilityResult> EvaluateStudentEligibilityAsync(int eventId, string studentUserId);
}

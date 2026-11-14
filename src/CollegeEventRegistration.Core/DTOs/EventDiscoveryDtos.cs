using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Query parameters and filtering options for the Student Event Discovery portal.
/// </summary>
public class StudentEventQueryCriteria
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public EventMode? EventMode { get; set; }
    public EventType? EventType { get; set; }

    /// <summary>
    /// Quick date filter: "all", "today", "this_week", "this_month", "custom"
    /// </summary>
    public string? DateFilter { get; set; } = "all";

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Only display events whose registration window is currently open (RegistrationStartDate <= now <= RegistrationEndDate).
    /// </summary>
    public bool OnlyRegistrationOpen { get; set; } = false;

    /// <summary>
    /// Sorting option: "upcoming" (default), "latest", "date_desc", "title_asc", "title_desc"
    /// </summary>
    public string? SortBy { get; set; } = "upcoming";

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

/// <summary>
/// Evaluation result of an authenticated student's eligibility for an event.
/// </summary>
public class StudentEventEligibilityResult
{
    public bool IsEligible { get; set; } = true;
    public string Status { get; set; } = "Eligible";
    public string Message { get; set; } = "You are eligible to attend this event.";
    public bool HasRestrictions { get; set; } = false;

    public string? EligibleDepartmentsDescription { get; set; }
    public string? YearOfStudyDescription { get; set; }
    public string? MinimumCgpaDescription { get; set; }
    public string? CollegeScopeDescription { get; set; }
    public string? SpecialRemarks { get; set; }
    public List<string> CriteriaBreakdown { get; set; } = new();
}

/// <summary>
/// Event category with dynamic count of published, upcoming events for filter badges.
/// </summary>
public class CategoryWithCountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IconClass { get; set; }
    public int PublishedCount { get; set; }
}

using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Specific departmental, year, or academic restrictions for event registration.
/// </summary>
public class EventEligibility : BaseEntity
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? YearOfStudy { get; set; }
    public decimal? MinimumCgpa { get; set; }
    public bool IsOpenToOtherColleges { get; set; } = true;
    public string? Remarks { get; set; }
}


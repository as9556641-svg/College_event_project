using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Academic or administrative department organizing events.
/// </summary>
public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? HeadOfDepartment { get; set; }

    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

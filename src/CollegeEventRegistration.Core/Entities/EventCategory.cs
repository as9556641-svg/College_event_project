using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Category or thematic classification for events (e.g. Workshop, Hackathon, Seminar).
/// </summary>
public class EventCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }

    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
}


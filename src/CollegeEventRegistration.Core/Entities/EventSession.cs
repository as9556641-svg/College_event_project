using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Individual session, lecture, or track within a multi-day or multi-part event.
/// </summary>
public class EventSession : BaseEntity
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string SessionTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SpeakerName { get; set; }
    public string? SpeakerDesignation { get; set; }
    public string? SpeakerOrganization { get; set; }

    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? VenueOrRoom { get; set; }
}

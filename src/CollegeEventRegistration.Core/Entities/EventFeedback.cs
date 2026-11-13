using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Post-event attendee evaluation and rating.
/// </summary>
public class EventFeedback : BaseEntity
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int OverallRating { get; set; }
    public int ContentQualityRating { get; set; }
    public int SpeakerRating { get; set; }
    public int VenueRating { get; set; }
    public string? Comments { get; set; }
    public bool IsAnonymous { get; set; } = false;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

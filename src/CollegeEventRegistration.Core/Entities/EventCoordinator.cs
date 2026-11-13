using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Junction entity associating multiple coordinators to an event, and an individual coordinator across multiple events.
/// </summary>
public class EventCoordinator : BaseEntity
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool IsPrimaryCoordinator { get; set; } = false;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

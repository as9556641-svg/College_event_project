using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// In-app notification and announcement dispatched to students, coordinators, or admins.
/// </summary>
public class Notification : BaseEntity
{
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public int? EventId { get; set; }
    public Event? Event { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public string? TargetRole { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
}


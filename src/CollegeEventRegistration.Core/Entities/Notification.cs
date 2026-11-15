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

    /// <summary>
    /// Optional direct link to relevant application page (e.g. /Student/Tickets, /Student/Certificates).
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Unique correlation/reference ID used for safe deduplication across retries and webhooks.
    /// E.g. REG-101, PAY-50, CERT-12, TKT-44.
    /// </summary>
    public string? CorrelationId { get; set; }
}


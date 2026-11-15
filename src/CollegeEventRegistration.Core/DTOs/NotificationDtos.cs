using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public int? EventId { get; set; }
    public string? EventTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? TargetRole { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.UtcNow - CreatedAt;
            if (elapsed.TotalMinutes < 1) return "Just now";
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
            return CreatedAt.ToString("MMM dd, yyyy");
        }
    }
}

public class CreateNotificationDto
{
    public string UserId { get; set; } = string.Empty;
    public int? EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.General;
    public string? TargetRole { get; set; }
    public string? ActionUrl { get; set; }
    public string? CorrelationId { get; set; }
}

public class NotificationSummaryDto
{
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public List<NotificationDto> RecentNotifications { get; set; } = new();
}

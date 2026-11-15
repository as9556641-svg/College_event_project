using System.ComponentModel.DataAnnotations;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

public class AnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; }
    public AnnouncementStatus Status { get; set; }
    public AnnouncementAudience AudienceType { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? TargetRole { get; set; }
    public int? EventId { get; set; }
    public string? EventTitle { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
}

public class CreateAnnouncementDto
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 5)]
    public string Content { get; set; } = string.Empty;

    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public AnnouncementAudience AudienceType { get; set; } = AnnouncementAudience.All;

    public int? DepartmentId { get; set; }
    public string? TargetRole { get; set; }
    public int? EventId { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public bool PublishImmediately { get; set; } = true;
}

public class EditAnnouncementDto
{
    public int Id { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 5)]
    public string Content { get; set; } = string.Empty;

    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public AnnouncementAudience AudienceType { get; set; } = AnnouncementAudience.All;

    public int? DepartmentId { get; set; }
    public string? TargetRole { get; set; }
    public int? EventId { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class AnnouncementFilterDto
{
    public string? SearchTerm { get; set; }
    public AnnouncementStatus? Status { get; set; }
    public AnnouncementPriority? Priority { get; set; }
    public AnnouncementAudience? AudienceType { get; set; }
    public int? DepartmentId { get; set; }
    public int? EventId { get; set; }
}

public class AnnouncementResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AnnouncementDto? Announcement { get; set; }

    public static AnnouncementResultDto Ok(AnnouncementDto announcement, string message = "Success") =>
        new() { Success = true, Announcement = announcement, Message = message };

    public static AnnouncementResultDto Fail(string message) =>
        new() { Success = false, Message = message };
}

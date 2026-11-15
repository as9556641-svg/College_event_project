using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Targeted campus and event announcements created by Administrators or Coordinators.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;
    public AnnouncementAudience AudienceType { get; set; } = AnnouncementAudience.All;

    /// <summary>
    /// If AudienceType == Department, the target department.
    /// </summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>
    /// If AudienceType == Role, the target role (e.g. "Student", "Coordinator").
    /// </summary>
    public string? TargetRole { get; set; }

    /// <summary>
    /// If AudienceType == EventParticipants, the target event.
    /// </summary>
    public int? EventId { get; set; }
    public Event? Event { get; set; }

    /// <summary>
    /// Author / creator of the announcement.
    /// </summary>
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser? AuthorUser { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

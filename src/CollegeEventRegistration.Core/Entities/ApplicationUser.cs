using Microsoft.AspNetCore.Identity;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Core IdentityUser with college and profile attributes.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? RollNumber { get; set; }
    public string? CollegeName { get; set; }
    public string? Department { get; set; }
    public int? YearOfStudy { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Prevents normal admin workflows from accidentally deleting, demoting, or locking out the root Super Admin.
    /// </summary>
    public bool IsSuperAdminProtected { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigation collections
    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
    public ICollection<EventCoordinator> CoordinatedEvents { get; set; } = new List<EventCoordinator>();
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    public ICollection<Attendance> VerifiedAttendances { get; set; } = new List<Attendance>();
    public ICollection<EventFeedback> SubmittedFeedbacks { get; set; } = new List<EventFeedback>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

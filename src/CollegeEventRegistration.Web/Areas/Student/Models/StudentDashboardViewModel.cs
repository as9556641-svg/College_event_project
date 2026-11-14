using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Areas.Student.Models;

/// <summary>
/// Data contract for Student Participation Portal scoped strictly to current student's registrations.
/// </summary>
public class StudentDashboardViewModel
{
    public ApplicationUser CurrentUser { get; set; } = null!;

    // Overview Metrics
    public int UpcomingRegisteredEventsCount { get; set; }
    public int TotalRegistrationsCount { get; set; }
    public int CompletedEventsCount { get; set; }
    public int AvailableCertificatesCount { get; set; }

    // Main Sections (Filtered strictly to current authenticated student)
    public List<EventRegistration> MyRegistrations { get; set; } = new();
    public List<Event> DiscoverEvents { get; set; } = new();
    public List<Attendance> AttendanceHistory { get; set; } = new();
}

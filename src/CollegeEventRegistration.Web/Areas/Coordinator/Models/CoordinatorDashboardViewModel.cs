using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Models;

/// <summary>
/// Data contract for Event Coordinator Dashboard scoped strictly to assigned events.
/// </summary>
public class CoordinatorDashboardViewModel
{
    public ApplicationUser CurrentUser { get; set; } = null!;

    // Overview Metrics
    public int AssignedEventsCount { get; set; }
    public int UpcomingEventsCount { get; set; }
    public int OngoingEventsCount { get; set; }
    public int CompletedEventsCount { get; set; }
    public int TotalParticipantsCount { get; set; }
    public int CheckedInCount { get; set; }

    // Main Sections (Filtered strictly to assigned events)
    public List<Event> AssignedEvents { get; set; } = new();
    public List<EventSession> UpcomingSessions { get; set; } = new();
    public List<EventRegistration> RecentRegistrations { get; set; } = new();
}

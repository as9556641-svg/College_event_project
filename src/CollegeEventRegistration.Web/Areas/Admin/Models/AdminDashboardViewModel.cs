using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Areas.Admin.Models;

/// <summary>
/// Data contract for Institutional Admin Dashboard with real database-backed metrics.
/// </summary>
public class AdminDashboardViewModel
{
    public ApplicationUser CurrentUser { get; set; } = null!;

    // Overview Metrics
    public int TotalEvents { get; set; }
    public int PendingApprovals { get; set; }
    public int ApprovedEvents { get; set; }
    public int RejectedEvents { get; set; }
    public int TotalRegistrations { get; set; }
    public int ActiveCoordinators { get; set; }

    // Main Sections
    public List<Event> RecentEvents { get; set; } = new();
    public List<Event> EventsAwaitingApproval { get; set; } = new();
    public List<AuditLog> RecentAdminActivity { get; set; } = new();
    public List<DepartmentMetricDto> DepartmentBreakdown { get; set; } = new();
}

public class DepartmentMetricDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int EventCount { get; set; }
}

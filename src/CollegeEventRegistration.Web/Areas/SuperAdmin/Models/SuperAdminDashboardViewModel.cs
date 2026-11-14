using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Areas.SuperAdmin.Models;

/// <summary>
/// Data contract for SuperAdmin Governance Dashboard with real database-backed metrics.
/// </summary>
public class SuperAdminDashboardViewModel
{
    public ApplicationUser CurrentUser { get; set; } = null!;
    public int TotalUsers { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalCategories { get; set; }
    public int TotalEvents { get; set; }
    public int PendingApprovals { get; set; }
    public string SystemStatus { get; set; } = "Operational";
    public string DatabaseEngine { get; set; } = "SQL Server 2025 Express";
    public List<AuditLog> RecentAuditLogs { get; set; } = new();
}

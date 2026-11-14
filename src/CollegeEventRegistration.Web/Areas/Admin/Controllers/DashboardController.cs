using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Count active users in Coordinator role
        var coordinators = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Coordinator));

        var viewModel = new AdminDashboardViewModel
        {
            CurrentUser = currentUser,
            TotalEvents = await _context.Events.CountAsync(e => !e.IsDeleted),
            PendingApprovals = await _context.Events.CountAsync(e => !e.IsDeleted && e.Status == EventStatus.PendingApproval),
            ApprovedEvents = await _context.Events.CountAsync(e => !e.IsDeleted && (e.Status == EventStatus.Approved || e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing || e.Status == EventStatus.Completed)),
            RejectedEvents = await _context.Events.CountAsync(e => !e.IsDeleted && e.Status == EventStatus.Rejected),
            TotalRegistrations = await _context.EventRegistrations.CountAsync(),
            ActiveCoordinators = coordinators.Count,
            RecentEvents = await _context.Events
                .Include(e => e.Department)
                .Include(e => e.EventCategory)
                .Where(e => !e.IsDeleted)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .ToListAsync(),
            EventsAwaitingApproval = await _context.Events
                .Include(e => e.Department)
                .Include(e => e.EventCategory)
                .Include(e => e.CreatedByUser)
                .Where(e => !e.IsDeleted && e.Status == EventStatus.PendingApproval)
                .OrderBy(e => e.CreatedAt)
                .Take(5)
                .ToListAsync(),
            RecentAdminActivity = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync(),
            DepartmentBreakdown = await _context.Departments
                .Where(d => d.IsActive)
                .Select(d => new DepartmentMetricDto
                {
                    DepartmentName = d.Name,
                    DepartmentCode = d.Code,
                    EventCount = d.Events.Count(e => !e.IsDeleted)
                })
                .OrderByDescending(d => d.EventCount)
                .Take(5)
                .ToListAsync()
        };

        // Populate ViewBag for backwards compatibility
        ViewBag.TotalEvents = viewModel.TotalEvents;
        ViewBag.PendingApproval = viewModel.PendingApprovals;
        ViewBag.TotalRegistrations = viewModel.TotalRegistrations;

        return View(viewModel);
    }
}

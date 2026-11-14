using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
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

        var viewModel = new SuperAdminDashboardViewModel
        {
            CurrentUser = currentUser,
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalDepartments = await _context.Departments.CountAsync(),
            TotalCategories = await _context.EventCategories.CountAsync(),
            TotalEvents = await _context.Events.CountAsync(),
            PendingApprovals = await _context.Events.CountAsync(e => e.Status == EventStatus.PendingApproval),
            SystemStatus = "System Operational",
            DatabaseEngine = "SQL Server 2025 Express",
            RecentAuditLogs = await _context.AuditLogs
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync()
        };

        // Populate ViewBag for backwards compatibility with any existing tests
        ViewBag.TotalUsers = viewModel.TotalUsers;
        ViewBag.TotalDepartments = viewModel.TotalDepartments;
        ViewBag.TotalCategories = viewModel.TotalCategories;
        ViewBag.TotalEvents = viewModel.TotalEvents;

        return View(viewModel);
    }
}

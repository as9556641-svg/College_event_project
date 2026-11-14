using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.Coordinator.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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

        // Query assigned events for the current coordinator ONLY
        var assignedEventIds = await _context.EventCoordinators
            .Where(ec => ec.UserId == currentUser.Id)
            .Select(ec => ec.EventId)
            .ToListAsync();

        var viewModel = new CoordinatorDashboardViewModel
        {
            CurrentUser = currentUser,
            AssignedEventsCount = assignedEventIds.Count,
            UpcomingEventsCount = await _context.Events
                .CountAsync(e => assignedEventIds.Contains(e.Id) && !e.IsDeleted && e.EventStartDate > DateTime.UtcNow),
            OngoingEventsCount = await _context.Events
                .CountAsync(e => assignedEventIds.Contains(e.Id) && !e.IsDeleted && (e.Status == EventStatus.Ongoing || (e.EventStartDate <= DateTime.UtcNow && e.EventEndDate >= DateTime.UtcNow))),
            CompletedEventsCount = await _context.Events
                .CountAsync(e => assignedEventIds.Contains(e.Id) && !e.IsDeleted && (e.Status == EventStatus.Completed || e.EventEndDate < DateTime.UtcNow)),
            TotalParticipantsCount = await _context.EventRegistrations
                .CountAsync(r => assignedEventIds.Contains(r.EventId)),
            CheckedInCount = await _context.Attendances
                .CountAsync(a => assignedEventIds.Contains(a.Registration.EventId) && a.Status == AttendanceStatus.CheckedIn),
            AssignedEvents = await _context.Events
                .Include(e => e.Department)
                .Include(e => e.EventCategory)
                .Where(e => assignedEventIds.Contains(e.Id) && !e.IsDeleted)
                .OrderByDescending(e => e.EventStartDate)
                .Take(5)
                .ToListAsync(),
            UpcomingSessions = await _context.EventSessions
                .Include(s => s.Event)
                .Where(s => assignedEventIds.Contains(s.EventId) && s.SessionDate >= DateTime.UtcNow.Date)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.StartTime)
                .Take(5)
                .ToListAsync(),
            RecentRegistrations = await _context.EventRegistrations
                .Include(r => r.User)
                .Include(r => r.Event)
                .Where(r => assignedEventIds.Contains(r.EventId))
                .OrderByDescending(r => r.RegisteredAt)
                .Take(5)
                .ToListAsync()
        };

        // Populate ViewBag for backwards compatibility
        ViewBag.CoordinatedCount = viewModel.AssignedEventsCount;

        return View(viewModel);
    }
}

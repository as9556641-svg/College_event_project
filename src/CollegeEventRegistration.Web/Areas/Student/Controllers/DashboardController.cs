using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.Student.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
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

        var viewModel = new StudentDashboardViewModel
        {
            CurrentUser = currentUser,
            UpcomingRegisteredEventsCount = await _context.EventRegistrations
                .CountAsync(r => r.UserId == currentUser.Id && !r.Event.IsDeleted && r.Status == RegistrationStatus.Confirmed && r.Event.EventStartDate > DateTime.UtcNow),
            TotalRegistrationsCount = await _context.EventRegistrations
                .CountAsync(r => r.UserId == currentUser.Id && !r.Event.IsDeleted),
            CompletedEventsCount = await _context.EventRegistrations
                .CountAsync(r => r.UserId == currentUser.Id && !r.Event.IsDeleted && (r.Event.Status == EventStatus.Completed || r.Event.EventEndDate < DateTime.UtcNow)),
            AvailableCertificatesCount = await _context.Attendances
                .CountAsync(a => a.Registration.UserId == currentUser.Id && a.Status == AttendanceStatus.CheckedIn && a.Registration.Event.CertificateTemplate != null),
            MyRegistrations = await _context.EventRegistrations
                .Include(r => r.Event)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Event)
                    .ThenInclude(e => e.EventCategory)
                .Include(r => r.Ticket)
                .Where(r => r.UserId == currentUser.Id && !r.Event.IsDeleted)
                .OrderByDescending(r => r.RegisteredAt)
                .Take(5)
                .ToListAsync(),
            DiscoverEvents = await _context.Events
                .Include(e => e.Department)
                .Include(e => e.EventCategory)
                .Where(e => !e.IsDeleted && (e.Status == EventStatus.Published || e.Status == EventStatus.Approved) && e.EventStartDate >= DateTime.UtcNow)
                .OrderBy(e => e.EventStartDate)
                .Take(4)
                .ToListAsync(),
            AttendanceHistory = await _context.Attendances
                .Include(a => a.Registration)
                    .ThenInclude(r => r.Event)
                .Where(a => a.Registration.UserId == currentUser.Id)
                .OrderByDescending(a => a.CheckedInAt)
                .Take(5)
                .ToListAsync()
        };

        // Populate ViewBag for backwards compatibility
        ViewBag.RegistrationsCount = viewModel.TotalRegistrationsCount;
        ViewBag.ConfirmedCount = await _context.EventRegistrations
            .CountAsync(r => r.UserId == currentUser.Id && r.Status == RegistrationStatus.Confirmed);

        return View(viewModel);
    }
}

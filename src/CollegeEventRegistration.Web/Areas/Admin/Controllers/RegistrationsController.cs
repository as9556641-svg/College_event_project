using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

/// <summary>
/// Administrative registration governance controller for Admins and SuperAdmins.
/// Provides institution-wide registration monitoring, search, filtering, and status management.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class RegistrationsController : Controller
{
    private readonly IEventRegistrationService _registrationService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegistrationsController> _logger;

    public RegistrationsController(
        IEventRegistrationService registrationService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<RegistrationsController> logger)
    {
        _registrationService = registrationService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Admin/Registrations
    /// Master institutional registration dashboard with cross-event search and filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RegistrationFilterCriteria criteria)
    {
        criteria ??= new RegistrationFilterCriteria();

        // Populate dropdowns for event and department filters
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title, e.RegisteredCount, e.TotalCapacity })
            .ToListAsync();

        var departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync();

        ViewBag.EventsList = events
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = $"{e.Title} ({e.RegisteredCount}/{e.TotalCapacity})",
                Selected = criteria.EventId.HasValue && criteria.EventId.Value == e.Id
            })
            .ToList();

        ViewBag.DepartmentsList = departments
            .Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = $"{d.Name} ({d.Code})",
                Selected = criteria.DepartmentId.HasValue && criteria.DepartmentId.Value == d.Id
            })
            .ToList();

        var pagedResult = await _registrationService.GetRegistrationsForAdminAsync(criteria);

        ViewBag.Criteria = criteria;
        return View(pagedResult);
    }

    /// <summary>
    /// GET: /Admin/Registrations/Details/{id}
    /// Full registration inspection.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var role = User.IsInRole(nameof(UserRole.SuperAdmin)) ? nameof(UserRole.SuperAdmin) : nameof(UserRole.Admin);
        var detail = await _registrationService.GetRegistrationDetailsForStaffAsync(id, currentUser.Id, role);

        if (detail == null)
        {
            return NotFound();
        }

        return View(detail);
    }

    /// <summary>
    /// POST: /Admin/Registrations/UpdateStatus
    /// Administrative override and state transition for registrations.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromForm] UpdateRegistrationStatusDto dto, [FromQuery] int? returnEventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var role = User.IsInRole(nameof(UserRole.SuperAdmin)) ? nameof(UserRole.SuperAdmin) : nameof(UserRole.Admin);
        var (success, message) = await _registrationService.UpdateRegistrationStatusAsync(
            dto.RegistrationId,
            dto.NewStatus,
            currentUser.Id,
            role,
            dto.Remarks
        );

        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Index), new { EventId = returnEventId });
    }
}

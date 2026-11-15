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

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

/// <summary>
/// Coordinator registration and participant management controller.
/// Strictly scoped to events where the coordinator is assigned or was the creator.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Coordinator/Registrations
    /// Lists participant registrations for an event coordinated by the user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RegistrationFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        criteria ??= new RegistrationFilterCriteria();

        // 1. Fetch all events coordinated by this user
        var coordinatedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.CreatedByUserId == currentUser.Id || e.Coordinators.Any(c => c.UserId == currentUser.Id)))
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.RegisteredCount, e.TotalCapacity, e.Status })
            .ToListAsync();

        ViewBag.CoordinatedEvents = coordinatedEvents
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = $"{e.Title} ({e.RegisteredCount}/{e.TotalCapacity} seats - {e.Status})",
                Selected = criteria.EventId.HasValue && criteria.EventId.Value == e.Id
            })
            .ToList();

        // If no event selected but coordinator has events, pick the first
        if (!criteria.EventId.HasValue && coordinatedEvents.Any())
        {
            criteria.EventId = coordinatedEvents.First().Id;
        }

        PagedResult<RegistrationSummaryDto> result;

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            try
            {
                result = await _registrationService.GetRegistrationsForCoordinatorAsync(
                    criteria.EventId.Value,
                    currentUser.Id,
                    criteria
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized registration access attempt by coordinator {UserId}", currentUser.Id);
                TempData["ErrorMessage"] = "You are not authorized to view registrations for that event.";
                result = new PagedResult<RegistrationSummaryDto> { Items = new List<RegistrationSummaryDto>() };
            }
        }
        else
        {
            result = new PagedResult<RegistrationSummaryDto> { Items = new List<RegistrationSummaryDto>() };
        }

        ViewBag.Criteria = criteria;
        return View(result);
    }

    /// <summary>
    /// POST: /Coordinator/Registrations/UpdateStatus
    /// Updates the status of a participant registration (Pending, Confirmed, Waitlisted, Cancelled).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus([FromForm] UpdateRegistrationStatusDto dto, [FromForm] int returnEventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var (success, message) = await _registrationService.UpdateRegistrationStatusAsync(
            dto.RegistrationId,
            dto.NewStatus,
            currentUser.Id,
            nameof(UserRole.Coordinator),
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

        return RedirectToAction(nameof(Index), new { eventId = returnEventId });
    }
}

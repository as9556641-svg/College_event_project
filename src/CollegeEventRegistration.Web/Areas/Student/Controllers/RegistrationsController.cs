using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Student registration management controller.
/// Enforces strict student tenant isolation: students can only register, view,
/// and cancel their own event registrations.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class RegistrationsController : Controller
{
    private readonly IEventRegistrationService _registrationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegistrationsController> _logger;

    public RegistrationsController(
        IEventRegistrationService registrationService,
        UserManager<ApplicationUser> userManager,
        ILogger<RegistrationsController> logger)
    {
        _registrationService = registrationService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Registrations
    /// Lists all event registrations belonging to the authenticated student with status filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StudentRegistrationFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        criteria ??= new StudentRegistrationFilterCriteria();
        var pagedResult = await _registrationService.GetStudentRegistrationsAsync(currentUser.Id, criteria);

        ViewBag.Criteria = criteria;
        return View(pagedResult);
    }

    /// <summary>
    /// GET: /Student/Registrations/Details/{id}
    /// Displays comprehensive details of a specific registration pass.
    /// Strictly verifies student tenant isolation.
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

        var registration = await _registrationService.GetRegistrationDetailsForStudentAsync(id, currentUser.Id);
        if (registration == null)
        {
            _logger.LogWarning("Access denied or registration #{Id} not found for student {UserId}", id, currentUser.Id);
            return NotFound();
        }

        return View(registration);
    }

    /// <summary>
    /// POST: /Student/Registrations/Register
    /// Submits a student registration request for an eligible published event.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromForm] int eventId)
    {
        if (eventId <= 0)
        {
            TempData["ErrorMessage"] = "Invalid event identifier.";
            return RedirectToAction("Index", "Events");
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var result = await _registrationService.RegisterStudentAsync(eventId, currentUser.Id);

        if (result.Success)
        {
            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = result.RegistrationId });
        }

        TempData["ErrorMessage"] = result.Message;
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    /// <summary>
    /// POST: /Student/Registrations/Cancel/{id}
    /// Cancels a student registration and releases the event seat reservation.
    /// Strictly verifies student tenant isolation.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, [FromForm] string? cancellationReason)
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

        var (success, message) = await _registrationService.CancelStudentRegistrationAsync(
            id,
            currentUser.Id,
            cancellationReason ?? "Cancelled by student"
        );

        if (success)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}

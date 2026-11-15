using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class AnnouncementsController : Controller
{
    private readonly IAnnouncementService _announcementService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnnouncementsController> _logger;

    public AnnouncementsController(
        IAnnouncementService announcementService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AnnouncementsController> logger)
    {
        _announcementService = announcementService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _announcementService.GetAnnouncementsForCoordinatorAsync(currentUser.Id, page, 15);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? eventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var assignedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => e.Coordinators.Any(c => c.UserId == currentUser.Id) && !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title })
            .ToListAsync();

        ViewBag.AssignedEvents = assignedEvents;

        var dto = new CreateAnnouncementDto
        {
            AudienceType = AnnouncementAudience.EventParticipants,
            EventId = eventId ?? (assignedEvents.Any() ? assignedEvents.First().Id : null),
            Priority = AnnouncementPriority.Normal,
            PublishImmediately = true
        };

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAnnouncementDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Enforce coordinator scoping: must target event participants and an assigned event
        dto.AudienceType = AnnouncementAudience.EventParticipants;

        if (!dto.EventId.HasValue)
        {
            ModelState.AddModelError(nameof(dto.EventId), "You must select an assigned event for the announcement.");
        }

        if (!ModelState.IsValid)
        {
            var assignedEvents = await _context.Events
                .AsNoTracking()
                .Where(e => e.Coordinators.Any(c => c.UserId == currentUser.Id) && !e.IsDeleted)
                .OrderByDescending(e => e.EventStartDate)
                .Select(e => new { e.Id, e.Title })
                .ToListAsync();

            ViewBag.AssignedEvents = assignedEvents;
            return View(dto);
        }

        try
        {
            var result = await _announcementService.CreateAnnouncementAsync(dto, currentUser.Id, isStaff: true);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(dto);
            }

            TempData["SuccessMessage"] = "Announcement successfully published for event participants.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt by Coordinator {UserId} to create announcement", currentUser.Id);
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var announcement = await _announcementService.GetAnnouncementDetailsAsync(id, currentUser.Id);
        if (announcement == null) return NotFound();

        var dto = new EditAnnouncementDto
        {
            Id = announcement.Id,
            Title = announcement.Title,
            Content = announcement.Content,
            Priority = announcement.Priority,
            AudienceType = announcement.AudienceType,
            DepartmentId = announcement.DepartmentId,
            TargetRole = announcement.TargetRole,
            EventId = announcement.EventId,
            ExpiresAt = announcement.ExpiresAt
        };

        var assignedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => e.Coordinators.Any(c => c.UserId == currentUser.Id) && !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title })
            .ToListAsync();

        ViewBag.AssignedEvents = assignedEvents;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditAnnouncementDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        dto.Id = id;
        dto.AudienceType = AnnouncementAudience.EventParticipants;

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        try
        {
            var result = await _announcementService.UpdateAnnouncementAsync(id, dto, currentUser.Id, isStaff: true);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View(dto);
            }

            TempData["SuccessMessage"] = "Announcement updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _announcementService.PublishAnnouncementAsync(id, currentUser.Id, isStaff: true);
        TempData["SuccessMessage"] = "Announcement published.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _announcementService.ArchiveAnnouncementAsync(id, currentUser.Id, isStaff: true);
        TempData["SuccessMessage"] = "Announcement archived.";
        return RedirectToAction(nameof(Index));
    }
}

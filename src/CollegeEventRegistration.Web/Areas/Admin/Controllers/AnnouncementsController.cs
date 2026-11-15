using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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
    public async Task<IActionResult> Index(AnnouncementFilterDto filter, int page = 1)
    {
        var result = await _announcementService.GetAllAnnouncementsAdminAsync(filter, page, 15);

        ViewBag.Filter = filter;
        ViewBag.Departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync();

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new CreateAnnouncementDto
        {
            Priority = AnnouncementPriority.Normal,
            AudienceType = AnnouncementAudience.All,
            PublishImmediately = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAnnouncementDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var result = await _announcementService.CreateAnnouncementAsync(dto, currentUser.Id, isStaff: true);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await PopulateDropdownsAsync();
            return View(dto);
        }

        TempData["SuccessMessage"] = $"Announcement '{dto.Title}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var announcement = await _announcementService.GetAnnouncementDetailsAsync(id, currentUser.Id);
        if (announcement == null) return NotFound();

        await PopulateDropdownsAsync();

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

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditAnnouncementDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        dto.Id = id;

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(dto);
        }

        var result = await _announcementService.UpdateAnnouncementAsync(id, dto, currentUser.Id, isStaff: true);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await PopulateDropdownsAsync();
            return View(dto);
        }

        TempData["SuccessMessage"] = "Announcement updated successfully.";
        return RedirectToAction(nameof(Index));
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _announcementService.DeleteAnnouncementAsync(id, currentUser.Id, isStaff: true);
        TempData["SuccessMessage"] = "Announcement deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync();

        ViewBag.Events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title })
            .ToListAsync();
    }
}

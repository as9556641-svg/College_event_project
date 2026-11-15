using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class AnnouncementsController : Controller
{
    private readonly IAnnouncementService _announcementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnnouncementsController> _logger;

    public AnnouncementsController(
        IAnnouncementService announcementService,
        UserManager<ApplicationUser> userManager,
        ILogger<AnnouncementsController> logger)
    {
        _announcementService = announcementService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _announcementService.GetActiveAnnouncementsForStudentAsync(currentUser.Id, page, 10);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var announcement = await _announcementService.GetAnnouncementDetailsAsync(id, currentUser.Id);
        if (announcement == null || announcement.Status != AnnouncementStatus.Published)
        {
            return NotFound();
        }

        return View(announcement);
    }
}

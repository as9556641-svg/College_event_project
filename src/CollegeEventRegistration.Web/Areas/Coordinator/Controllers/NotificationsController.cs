using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, bool unreadOnly = false)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _notificationService.GetUserNotificationsAsync(currentUser.Id, page, 15, unreadOnly);
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.TotalUnread = await _notificationService.GetUnreadCountAsync(currentUser.Id);

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            await _notificationService.MarkAsReadAsync(id, currentUser.Id);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _notificationService.MarkAllAsReadAsync(currentUser.Id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Json(new { unreadCount = 0 });

        var count = await _notificationService.GetUnreadCountAsync(currentUser.Id);
        return Json(new { unreadCount = count });
    }
}

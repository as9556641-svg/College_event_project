using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Web.Areas.Student.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Dedicated Student Event Discovery Portal controller.
/// Provides rich catalog search, multi-criteria filtering, server-side pagination,
/// and comprehensive event details with strict server-side Published-only visibility.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class EventsController : Controller
{
    private readonly IEventDiscoveryService _discoveryService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventDiscoveryService discoveryService,
        UserManager<ApplicationUser> userManager,
        ILogger<EventsController> logger)
    {
        _discoveryService = discoveryService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Events
    /// Dedicated event discovery catalog with live database search, multi-faceted filtering, and server-side pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StudentEventQueryCriteria criteria)
    {
        criteria ??= new StudentEventQueryCriteria();

        // 1. Fetch paginated published events
        var pagedEntities = await _discoveryService.GetPublishedEventsAsync(criteria);

        // 2. Fetch active categories with counts and active departments for filter sidebars
        var categories = await _discoveryService.GetActiveCategoriesWithCountAsync();
        var departments = await _discoveryService.GetActiveDepartmentsAsync();

        // 3. Resolve authenticated user
        var currentUser = await _userManager.GetUserAsync(User);

        // 4. Map to strongly typed ViewModels
        var cardViewModels = pagedEntities.Items
            .Select(StudentEventCardViewModel.FromEntity)
            .ToList();

        var viewModel = new StudentEventCatalogViewModel
        {
            Criteria = criteria,
            Events = new PagedResult<StudentEventCardViewModel>
            {
                Items = cardViewModels,
                TotalCount = pagedEntities.TotalCount,
                PageNumber = pagedEntities.PageNumber,
                PageSize = pagedEntities.PageSize
            },
            Categories = categories,
            Departments = departments,
            CurrentUser = currentUser
        };

        return View(viewModel);
    }

    /// <summary>
    /// GET: /Student/Events/Details/{id}
    /// Detailed student-facing overview of a published event, its sessions, and student eligibility.
    /// Returns 404 NotFound if the event is not in Published status or has been soft-deleted.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        var evt = await _discoveryService.GetPublishedEventDetailsAsync(id);
        if (evt == null)
        {
            _logger?.LogWarning("Access denied or event not found for event ID {Id} (Non-published or non-existent)", id);
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserId = currentUser?.Id ?? string.Empty;

        var eligibilityResult = await _discoveryService.EvaluateStudentEligibilityAsync(id, currentUserId);
        var viewModel = StudentEventDetailsViewModel.FromEntity(evt, eligibilityResult, currentUser);

        return View(viewModel);
    }
}

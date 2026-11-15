using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

/// <summary>
/// Coordinator Feedback &amp; Ratings Management Controller.
/// Strictly enforces event-level coordinator scoping: coordinators can only review feedback,
/// rating metrics, and student comments for events they are assigned to coordinate.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class FeedbackController : Controller
{
    private readonly IFeedbackService _feedbackService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(
        IFeedbackService feedbackService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<FeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Coordinator/Feedback?eventId={eventId}
    /// Event feedback dashboard showing aggregate satisfaction scores, 5-star histogram,
    /// and student qualitative comments with event-scoped authorization.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? eventId,
        [FromQuery] FeedbackFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // 1. Retrieve all events assigned to this coordinator
        var coordinatedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.CreatedByUserId == currentUser.Id || e.Coordinators.Any(c => c.UserId == currentUser.Id)))
            .OrderByDescending(e => e.EventStartDate)
            .ToListAsync();

        if (!coordinatedEvents.Any())
        {
            ViewBag.CoordinatedEvents = new List<SelectListItem>();
            return View(null);
        }

        // 2. Resolve target event ID
        var targetEventId = eventId ?? criteria?.EventId ?? coordinatedEvents.First().Id;

        // 3. Strict Event-Level Scoping Security Check
        var isAuthorized = coordinatedEvents.Any(e => e.Id == targetEventId);
        if (!isAuthorized)
        {
            _logger.LogWarning("Security Violation: Coordinator {UserId} attempted to access Feedback for unassigned Event #{EventId}",
                currentUser.Id, targetEventId);

            TempData["ErrorMessage"] = "Access Denied: You are not authorized to view feedback for this event.";
            return RedirectToAction(nameof(Index), new { eventId = coordinatedEvents.First().Id });
        }

        // 4. Retrieve feedback summary and paged review items
        criteria ??= new FeedbackFilterCriteria();
        criteria.EventId = targetEventId;

        var summary = await _feedbackService.GetEventFeedbackSummaryAsync(
            targetEventId,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        var pagedFeedbacks = await _feedbackService.GetFeedbackRecordsAsync(
            criteria,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        ViewBag.CoordinatedEvents = coordinatedEvents.Select(e => new SelectListItem
        {
            Value = e.Id.ToString(),
            Text = $"{e.Title} ({e.EventStartDate:MMM dd, yyyy})",
            Selected = e.Id == targetEventId
        }).ToList();

        ViewBag.SelectedEventId = targetEventId;
        ViewBag.SelectedEvent = coordinatedEvents.First(e => e.Id == targetEventId);
        ViewBag.Summary = summary;
        ViewBag.Criteria = criteria;

        return View(pagedFeedbacks);
    }

    /// <summary>
    /// GET: /Coordinator/Feedback/Details/{id}
    /// Detailed inspection of an individual feedback submission.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var feedback = await _feedbackService.GetFeedbackByIdAsync(
            id,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        if (feedback == null)
        {
            TempData["ErrorMessage"] = "Feedback record not found or access denied.";
            return RedirectToAction(nameof(Index));
        }

        return View(feedback);
    }
}

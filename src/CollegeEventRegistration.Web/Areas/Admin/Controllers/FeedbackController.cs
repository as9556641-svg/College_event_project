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

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

/// <summary>
/// Administrator Feedback &amp; Ratings Management Controller.
/// Provides institution-wide student satisfaction oversight, multi-department filtering,
/// average rating analytics, and qualitative comment auditing.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Admin/Feedback
    /// Campus-wide feedback ledger with multi-criteria filtering across all departments and events.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FeedbackFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new FeedbackFilterCriteria();

        // Populate dropdowns for filtering
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = e.Title,
                Selected = criteria.EventId == e.Id
            })
            .ToListAsync();

        var departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = d.Name,
                Selected = criteria.DepartmentId == d.Id
            })
            .ToListAsync();

        // Calculate campus-wide statistics
        var totalFeedbacks = await _context.EventFeedbacks.CountAsync();
        var avgCampusRating = totalFeedbacks > 0
            ? Math.Round(await _context.EventFeedbacks.AverageAsync(f => (double)f.OverallRating), 2)
            : 0.0;

        var totalAttendedRegistrations = await _context.Attendances
            .CountAsync(a => a.Status == AttendanceStatus.CheckedIn);

        var feedbackRate = totalAttendedRegistrations > 0
            ? Math.Round((double)totalFeedbacks / totalAttendedRegistrations * 100, 1)
            : 0.0;

        var pagedFeedbacks = await _feedbackService.GetFeedbackRecordsAsync(
            criteria,
            currentUser.Id,
            nameof(UserRole.Admin));

        ViewBag.Events = events;
        ViewBag.Departments = departments;
        ViewBag.Criteria = criteria;
        ViewBag.TotalFeedbacks = totalFeedbacks;
        ViewBag.AvgCampusRating = avgCampusRating;
        ViewBag.FeedbackRate = feedbackRate;
        ViewBag.TotalAttended = totalAttendedRegistrations;

        return View(pagedFeedbacks);
    }

    /// <summary>
    /// GET: /Admin/Feedback/Details/{id}
    /// Detailed administrative inspection of an individual evaluation.
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
            nameof(UserRole.Admin));

        if (feedback == null)
        {
            TempData["ErrorMessage"] = "Feedback record not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(feedback);
    }
}

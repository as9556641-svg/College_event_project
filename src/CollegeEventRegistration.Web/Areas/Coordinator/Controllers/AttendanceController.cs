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
/// Coordinator Attendance Management Controller.
/// Provides live QR camera scanning, duplicate-protected check-in,
/// manual roll entry with justification auditing, and event-scoped attendance tracking.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class AttendanceController : Controller
{
    private readonly IAttendanceService _attendanceService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AttendanceController> logger)
    {
        _attendanceService = attendanceService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Coordinator/Attendance?eventId={eventId}
    /// Event attendance dashboard showing metrics, checked-in students list, and search.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? eventId,
        [FromQuery] AttendanceFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // 1. Retrieve all events assigned to this coordinator
        var coordinatedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.CreatedByUserId == currentUser.Id || e.Coordinators.Any(c => c.UserId == currentUser.Id)))
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.Status, e.EventStartDate, e.EventType })
            .ToListAsync();

        if (!coordinatedEvents.Any())
        {
            ViewBag.CoordinatedEvents = new List<SelectListItem>();
            ViewBag.HasEvents = false;
            return View("NoEventsAssigned");
        }

        ViewBag.HasEvents = true;

        // Resolve active target event
        int activeEventId;
        if (eventId.HasValue && eventId.Value > 0)
        {
            activeEventId = eventId.Value;
        }
        else if (criteria?.EventId.HasValue == true && criteria.EventId.Value > 0)
        {
            activeEventId = criteria.EventId.Value;
        }
        else
        {
            activeEventId = coordinatedEvents.First().Id;
        }

        // Verify ownership/assignment
        if (!coordinatedEvents.Any(e => e.Id == activeEventId))
        {
            TempData["ErrorMessage"] = "You are not authorized to manage attendance for that event.";
            activeEventId = coordinatedEvents.First().Id;
        }

        ViewBag.ActiveEventId = activeEventId;
        ViewBag.CoordinatedEvents = coordinatedEvents
            .Select(e => new SelectListItem(e.Title, e.Id.ToString(), e.Id == activeEventId))
            .ToList();

        // 2. Fetch event summary metrics
        EventAttendanceSummaryDto? summary = null;
        try
        {
            summary = await _attendanceService.GetEventAttendanceSummaryAsync(
                activeEventId,
                currentUser.Id,
                nameof(UserRole.Coordinator));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve attendance summary for Event #{EventId}", activeEventId);
        }

        ViewBag.Summary = summary;

        // 3. Fetch attendance records
        criteria ??= new AttendanceFilterCriteria();
        criteria.EventId = activeEventId;

        var records = await _attendanceService.GetAttendanceRecordsAsync(
            criteria,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        ViewBag.FilterCriteria = criteria;
        return View(records);
    }

    /// <summary>
    /// GET: /Coordinator/Attendance/Scanner?eventId={eventId}
    /// Full live webcam and mobile rear camera QR scanner view.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Scanner([FromQuery] int eventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (eventId <= 0)
        {
            TempData["ErrorMessage"] = "Please select an event to launch the attendance scanner.";
            return RedirectToAction(nameof(Index));
        }

        var isAuthorized = await _attendanceService.CanStaffManageEventAttendanceAsync(
            eventId,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        if (!isAuthorized)
        {
            TempData["ErrorMessage"] = "You are not authorized to take attendance for this event.";
            return RedirectToAction(nameof(Index));
        }

        var ev = await _context.Events
            .AsNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

        if (ev == null)
        {
            TempData["ErrorMessage"] = "The specified event was not found.";
            return RedirectToAction(nameof(Index));
        }

        var summary = await _attendanceService.GetEventAttendanceSummaryAsync(
            eventId,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        ViewBag.Event = ev;
        ViewBag.Summary = summary;

        return View();
    }

    /// <summary>
    /// POST: /Coordinator/Attendance/CheckInQr
    /// Real-time AJAX endpoint called by the camera scanner when a QR code is detected.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInQr([FromBody] CheckInRequestDto request)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Unauthorized(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.UnauthorizedStaff,
                Message = "Session expired. Please log in again."
            });
        }

        if (request == null || request.EventId <= 0 || string.IsNullOrWhiteSpace(request.QrPayload))
        {
            return BadRequest(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "Invalid scan request payload."
            });
        }

        request.CheckInMethod = CheckInMethod.QrCode;

        var result = await _attendanceService.CheckInAsync(
            request,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        return Json(result);
    }

    /// <summary>
    /// POST: /Coordinator/Attendance/ManualCheckIn
    /// Processes fallback manual roll number or registration number check-in.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualCheckIn([FromBody] CheckInRequestDto request)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Unauthorized(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.UnauthorizedStaff,
                Message = "Session expired. Please log in again."
            });
        }

        if (request == null || request.EventId <= 0)
        {
            return BadRequest(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "Event ID is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RollNumber) &&
            string.IsNullOrWhiteSpace(request.RegistrationNumber) &&
            string.IsNullOrWhiteSpace(request.TicketCode))
        {
            return BadRequest(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "Please provide a Student Roll Number, Registration Number, or Ticket Code."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            return BadRequest(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "A reason or justification is strictly required for manual check-in."
            });
        }

        request.CheckInMethod = CheckInMethod.Manual;

        var result = await _attendanceService.CheckInAsync(
            request,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        return Json(result);
    }

    /// <summary>
    /// GET: /Coordinator/Attendance/Stats?eventId={eventId}
    /// Live JSON polling endpoint for scanner interface to update live metrics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Stats([FromQuery] int eventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        try
        {
            var summary = await _attendanceService.GetEventAttendanceSummaryAsync(
                eventId,
                currentUser.Id,
                nameof(UserRole.Coordinator));

            return Json(new
            {
                totalRegistered = summary.TotalRegistered,
                totalCheckedIn = summary.TotalCheckedIn,
                pendingCheckIn = summary.PendingCheckIn,
                checkInPercentage = summary.CheckInPercentage
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stats for Event #{EventId}", eventId);
            return BadRequest(new { error = ex.Message });
        }
    }
}

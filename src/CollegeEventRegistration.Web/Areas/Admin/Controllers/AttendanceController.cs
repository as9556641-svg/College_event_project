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
/// Administrator Attendance Management Controller.
/// Provides cross-campus attendance oversight, institutional auditing,
/// multi-event filtering, and administrative attendance inspection.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Admin/Attendance
    /// Cross-campus master attendance ledger.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AttendanceFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new AttendanceFilterCriteria();

        // Populate dropdowns for filtering
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new SelectListItem(e.Title, e.Id.ToString(), criteria.EventId.HasValue && criteria.EventId.Value == e.Id))
            .ToListAsync();

        var departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), criteria.DepartmentId.HasValue && criteria.DepartmentId.Value == d.Id))
            .ToListAsync();

        ViewBag.Events = events;
        ViewBag.Departments = departments;
        ViewBag.FilterCriteria = criteria;

        // University-wide summary stats
        var totalCampusAttendances = await _context.Attendances
            .CountAsync(a => !a.Registration.Event.IsDeleted && a.Status == AttendanceStatus.CheckedIn);

        var totalConfirmedRegistrations = await _context.EventRegistrations
            .CountAsync(r => !r.Event.IsDeleted && r.Status == RegistrationStatus.Confirmed);

        var overallRate = totalConfirmedRegistrations > 0
            ? Math.Round((double)totalCampusAttendances / totalConfirmedRegistrations * 100.0, 1)
            : 0.0;

        ViewBag.TotalAttendances = totalCampusAttendances;
        ViewBag.TotalRegistrations = totalConfirmedRegistrations;
        ViewBag.OverallRate = overallRate;

        var records = await _attendanceService.GetAttendanceRecordsAsync(
            criteria,
            currentUser.Id,
            nameof(UserRole.Admin));

        return View(records);
    }

    /// <summary>
    /// GET: /Admin/Attendance/Details/{id}
    /// Detailed audit view of an attendance entry.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var record = await _attendanceService.GetAttendanceDetailsAsync(
            id,
            currentUser.Id,
            nameof(UserRole.Admin));

        if (record == null)
        {
            TempData["ErrorMessage"] = "Attendance record was not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(record);
    }

    /// <summary>
    /// POST: /Admin/Attendance/ManualCheckIn
    /// Administrative manual check-in for exceptional cases.
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
                Message = "Session expired."
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

        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            return BadRequest(new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "Remarks are mandatory for manual check-in."
            });
        }

        request.CheckInMethod = CheckInMethod.Manual;

        var result = await _attendanceService.CheckInAsync(
            request,
            currentUser.Id,
            nameof(UserRole.Admin));

        return Json(result);
    }
}

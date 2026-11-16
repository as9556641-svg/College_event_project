using System;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class AnalyticsController : Controller
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IReportExportService _reportExportService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        IReportExportService reportExportService,
        UserManager<ApplicationUser> userManager,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _reportExportService = reportExportService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Event-scoped Coordinator Analytics Dashboard.
    /// Strictly limits visibility to assigned events.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        filter ??= new AnalyticsFilterDto();

        try
        {
            var model = await _analyticsService.GetCoordinatorAnalyticsAsync(currentUser.Id, filter, cancellationToken);
            ViewBag.Filter = filter;
            return View(model);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Coordinator {UserId} denied access to unassigned event analytics.", currentUser.Id);
            return Forbid();
        }
    }

    /// <summary>
    /// Scoped event performance report for coordinator's assigned events.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EventPerformance(AnalyticsFilterDto filter, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        filter ??= new AnalyticsFilterDto();

        try
        {
            var report = await _analyticsService.GetEventPerformanceReportAsync(filter, page, pageSize, currentUser.Id, cancellationToken);
            ViewBag.Filter = filter;
            return View(report);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Coordinator {UserId} denied access to unassigned event performance.", currentUser.Id);
            return Forbid();
        }
    }

    /// <summary>
    /// Exports event-scoped data for coordinator in RFC 4180 CSV format.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string reportType, AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        filter ??= new AnalyticsFilterDto();
        byte[] csvBytes;
        string fileNamePrefix;

        try
        {
            switch (reportType?.ToLowerInvariant())
            {
                case "events":
                    csvBytes = await _reportExportService.ExportEventPerformanceCsvAsync(filter, currentUser.Id, cancellationToken);
                    fileNamePrefix = "event_performance";
                    break;
                case "registrations":
                    csvBytes = await _reportExportService.ExportRegistrationCsvAsync(filter, currentUser.Id, cancellationToken);
                    fileNamePrefix = "event_registrations";
                    break;
                case "attendance":
                    csvBytes = await _reportExportService.ExportAttendanceCsvAsync(filter, currentUser.Id, cancellationToken);
                    fileNamePrefix = "event_attendance";
                    break;
                case "feedback":
                    csvBytes = await _reportExportService.ExportFeedbackCsvAsync(filter, currentUser.Id, cancellationToken);
                    fileNamePrefix = "event_feedback";
                    break;
                case "certificates":
                    csvBytes = await _reportExportService.ExportCertificateCsvAsync(filter, currentUser.Id, cancellationToken);
                    fileNamePrefix = "event_certificates";
                    break;
                default:
                    return BadRequest("Invalid report type specified.");
            }

            var fileName = $"{fileNamePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Coordinator {UserId} denied export for unassigned event.", currentUser.Id);
            return Forbid();
        }
    }

    /// <summary>
    /// Exports event-scoped summary in QuestPDF format.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportPdf(AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        filter ??= new AnalyticsFilterDto();

        try
        {
            var pdfBytes = await _reportExportService.ExportExecutiveSummaryPdfAsync(filter, currentUser.Id, cancellationToken);
            var fileName = $"event_analytics_summary_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Coordinator {UserId} denied PDF export for unassigned event.", currentUser.Id);
            return Forbid();
        }
    }
}

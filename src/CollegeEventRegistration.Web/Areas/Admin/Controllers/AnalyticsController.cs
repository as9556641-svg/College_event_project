using System;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class AnalyticsController : Controller
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IReportExportService _reportExportService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        IReportExportService reportExportService,
        ApplicationDbContext context,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _reportExportService = reportExportService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Campus-wide analytics dashboard with KPIs and chart visualizations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        filter ??= new AnalyticsFilterDto();
        await PopulateFilterViewBagsAsync(filter, cancellationToken);

        var summary = await _analyticsService.GetCampusAnalyticsSummaryAsync(filter, cancellationToken);
        ViewBag.Filter = filter;
        return View(summary);
    }

    /// <summary>
    /// Event performance tabular analysis with pagination and search.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EventPerformance(AnalyticsFilterDto filter, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        filter ??= new AnalyticsFilterDto();
        await PopulateFilterViewBagsAsync(filter, cancellationToken);

        var report = await _analyticsService.GetEventPerformanceReportAsync(filter, page, pageSize, coordinatorUserId: null, cancellationToken);
        ViewBag.Filter = filter;
        return View(report);
    }

    /// <summary>
    /// Department comparison analysis.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DepartmentPerformance(AnalyticsFilterDto filter, CancellationToken cancellationToken = default)
    {
        filter ??= new AnalyticsFilterDto();
        await PopulateFilterViewBagsAsync(filter, cancellationToken);

        var report = await _analyticsService.GetDepartmentPerformanceReportAsync(filter, cancellationToken);
        ViewBag.Filter = filter;
        return View(report);
    }

    /// <summary>
    /// Exports filtered data in RFC 4180 CSV format with formula injection protection.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string reportType, AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        filter ??= new AnalyticsFilterDto();
        byte[] csvBytes;
        string fileNamePrefix;

        switch (reportType?.ToLowerInvariant())
        {
            case "events":
                csvBytes = await _reportExportService.ExportEventPerformanceCsvAsync(filter, null, cancellationToken);
                fileNamePrefix = "event_performance";
                break;
            case "departments":
                csvBytes = await _reportExportService.ExportDepartmentPerformanceCsvAsync(filter, cancellationToken);
                fileNamePrefix = "department_performance";
                break;
            case "registrations":
                csvBytes = await _reportExportService.ExportRegistrationCsvAsync(filter, null, cancellationToken);
                fileNamePrefix = "registrations";
                break;
            case "attendance":
                csvBytes = await _reportExportService.ExportAttendanceCsvAsync(filter, null, cancellationToken);
                fileNamePrefix = "attendance";
                break;
            case "financials":
                csvBytes = await _reportExportService.ExportFinancialCsvAsync(filter, cancellationToken);
                fileNamePrefix = "financial_ledger";
                break;
            case "feedback":
                csvBytes = await _reportExportService.ExportFeedbackCsvAsync(filter, null, cancellationToken);
                fileNamePrefix = "feedback_summary";
                break;
            case "certificates":
                csvBytes = await _reportExportService.ExportCertificateCsvAsync(filter, null, cancellationToken);
                fileNamePrefix = "certificates";
                break;
            default:
                return BadRequest("Invalid report type specified.");
        }

        var fileName = $"{fileNamePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Exports executive analytics summary in QuestPDF format.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportPdf(AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        filter ??= new AnalyticsFilterDto();
        var pdfBytes = await _reportExportService.ExportExecutiveSummaryPdfAsync(filter, null, cancellationToken);
        var fileName = $"executive_analytics_summary_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task PopulateFilterViewBagsAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken)
    {
        ViewBag.Departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        ViewBag.Categories = await _context.EventCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}

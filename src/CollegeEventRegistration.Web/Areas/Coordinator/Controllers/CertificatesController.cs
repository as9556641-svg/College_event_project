using System;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

/// <summary>
/// Coordinator Certificates Controller.
/// Enables event coordinators to manage certificate generation, template configuration,
/// batch issuance, and certificate ledgers scoped strictly to their assigned events.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class CertificatesController : Controller
{
    private readonly ICertificateService _certificateService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(
        ICertificateService certificateService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CertificatesController> logger)
    {
        _certificateService = certificateService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Coordinator/Certificates
    /// Displays certificates issued for coordinator-assigned events with filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CertificateFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Get coordinator's assigned events for filter dropdown
        var assignedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => e.Coordinators.Any(c => c.UserId == currentUser.Id))
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.EventEndDate, e.Status })
            .ToListAsync();

        ViewBag.AssignedEvents = assignedEvents;
        ViewBag.Criteria = criteria;

        var (items, totalCount) = await _certificateService.GetCertificatesLedgerAsync(criteria, currentUser.Id, isCoordinator: true);
        ViewBag.TotalCount = totalCount;

        return View(items);
    }

    /// <summary>
    /// GET: /Coordinator/Certificates/Details/{id}
    /// Displays details of an issued certificate within coordinator's assigned events.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            var cert = await _certificateService.GetCertificateByIdAsync(id, currentUser.Id, isStaff: true);
            if (cert == null) return NotFound();
            return View(cert);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// POST: /Coordinator/Certificates/BatchIssue/{eventId}
    /// Batch issues certificates for all eligible attendees of an assigned event.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchIssue(int eventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            var result = await _certificateService.BatchIssueCertificatesForEventAsync(eventId, currentUser.Id);
            TempData["SuccessMessage"] = $"Batch Issuance Completed! Issued: {result.IssuedCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}.";
            if (result.Messages.Any())
            {
                TempData["WarningMessage"] = string.Join("; ", result.Messages.Take(3));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Batch issuance failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    /// <summary>
    /// GET: /Coordinator/Certificates/Download/{id}
    /// Downloads the PDF certificate file (scoped to assigned events).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            var result = await _certificateService.DownloadCertificatePdfAsync(id, currentUser.Id, isStaff: true);
            if (result == null) return NotFound();

            return File(result.Value.FileBytes, "application/pdf", result.Value.FileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET: /Coordinator/Certificates/Template/{eventId}
    /// Configure certificate signatory and titles for an assigned event.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Template(int eventId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var ev = await _context.Events
            .Include(e => e.Coordinators)
            .Include(e => e.CertificateTemplate)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ev == null) return NotFound();

        // Enforce assignment
        if (!ev.Coordinators.Any(c => c.UserId == currentUser.Id))
        {
            return Forbid();
        }

        var dto = new CertificateTemplateUpsertDto
        {
            EventId = ev.Id,
            TemplateTitle = ev.CertificateTemplate?.TemplateTitle ?? $"Certificate of Completion - {ev.Title}",
            SignatoryName = ev.CertificateTemplate?.SignatoryName ?? "Dr. K. Sharma",
            SignatoryTitle = ev.CertificateTemplate?.SignatoryTitle ?? "Dean of Academic Affairs",
            CertificateCodePrefix = ev.CertificateTemplate?.CertificateCodePrefix ?? "VAP-CERT"
        };

        ViewBag.EventTitle = ev.Title;
        return View(dto);
    }

    /// <summary>
    /// POST: /Coordinator/Certificates/Template/{eventId}
    /// Saves the certificate template configuration.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Template(int eventId, CertificateTemplateUpsertDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var ev = await _context.Events
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ev == null) return NotFound();

        if (!ev.Coordinators.Any(c => c.UserId == currentUser.Id))
        {
            return Forbid();
        }

        dto.EventId = eventId;
        await _certificateService.UpsertTemplateAsync(dto, currentUser.Id);

        TempData["SuccessMessage"] = "Certificate template updated successfully!";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}

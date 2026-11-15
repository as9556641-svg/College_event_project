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

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin Certificates Controller.
/// Campus-wide administrative control over certificate generation, manual issuance,
/// revocation with mandatory audit logging, and certificate template configuration.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Admin/Certificates
    /// Campus-wide certificates ledger with department, event, status, and search filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CertificateFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Dropdown data
        ViewBag.Departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        ViewBag.Events = await _context.Events
            .AsNoTracking()
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.DepartmentId })
            .ToListAsync();

        ViewBag.Criteria = criteria;

        var (items, totalCount) = await _certificateService.GetCertificatesLedgerAsync(criteria, currentUser.Id, isCoordinator: false);
        ViewBag.TotalCount = totalCount;

        return View(items);
    }

    /// <summary>
    /// GET: /Admin/Certificates/Details/{id}
    /// Full view of certificate metadata, recipient, event, verification token, and revocation history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var cert = await _certificateService.GetCertificateByIdAsync(id, currentUser.Id, isStaff: true);
        if (cert == null) return NotFound();

        return View(cert);
    }

    /// <summary>
    /// POST: /Admin/Certificates/Issue/{registrationId}
    /// Manually issues a certificate for an eligible registration.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int registrationId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _certificateService.IssueCertificateAsync(registrationId, currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"Certificate '{result.CertificateNumber}' issued successfully!";
            if (result.CertificateId.HasValue)
            {
                return RedirectToAction(nameof(Details), new { id = result.CertificateId.Value });
            }
        }
        else
        {
            TempData["ErrorMessage"] = $"Issuance failed: {result.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// POST: /Admin/Certificates/BatchIssue/{eventId}
    /// Batch issues certificates for all eligible attendees of an event.
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
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Batch issuance failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    /// <summary>
    /// POST: /Admin/Certificates/Revoke
    /// Revokes an issued certificate with a mandatory reason.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int certificateId, string revocationReason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (string.IsNullOrWhiteSpace(revocationReason))
        {
            TempData["ErrorMessage"] = "A non-empty revocation reason is required.";
            return RedirectToAction(nameof(Details), new { id = certificateId });
        }

        var result = await _certificateService.RevokeCertificateAsync(certificateId, revocationReason, currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Certificate has been revoked.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Details), new { id = certificateId });
    }

    /// <summary>
    /// GET: /Admin/Certificates/Download/{id}
    /// Downloads the PDF certificate file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _certificateService.DownloadCertificatePdfAsync(id, currentUser.Id, isStaff: true);
        if (result == null) return NotFound();

        return File(result.Value.FileBytes, "application/pdf", result.Value.FileName);
    }

    /// <summary>
    /// GET: /Admin/Certificates/Template/{eventId}
    /// Configures the certificate template for an event.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Template(int eventId)
    {
        var ev = await _context.Events
            .Include(e => e.CertificateTemplate)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ev == null) return NotFound();

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
    /// POST: /Admin/Certificates/Template/{eventId}
    /// Saves the certificate template configuration.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Template(int eventId, CertificateTemplateUpsertDto dto)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        dto.EventId = eventId;
        await _certificateService.UpsertTemplateAsync(dto, currentUser.Id);

        TempData["SuccessMessage"] = "Certificate template updated successfully!";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}

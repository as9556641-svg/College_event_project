using System;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Student Certificates Controller.
/// Enables students to view their certificates of completion, claim certificates for attended events,
/// view credentials, and download genuine PDF certificates.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class CertificatesController : Controller
{
    private readonly ICertificateService _certificateService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(
        ICertificateService certificateService,
        UserManager<ApplicationUser> userManager,
        ILogger<CertificatesController> logger)
    {
        _certificateService = certificateService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Certificates
    /// Displays student certificate dashboard with eligible and issued certificates.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var certificates = await _certificateService.GetStudentCertificatesAsync(currentUser.Id);
        return View(certificates);
    }

    /// <summary>
    /// POST: /Student/Certificates/Claim/{registrationId}
    /// Claims and generates an official PDF certificate for an eligible attended event.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int registrationId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Verify eligibility and ownership first
        var eligibility = await _certificateService.CheckEligibilityAsync(registrationId, currentUser.Id);
        if (!eligibility.IsEligible && !eligibility.IsIssued)
        {
            TempData["ErrorMessage"] = eligibility.EligibilityReason;
            return RedirectToAction(nameof(Index));
        }

        var result = await _certificateService.IssueCertificateAsync(registrationId, currentUser.Id);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Certificate generated successfully!";
            if (result.CertificateId.HasValue)
            {
                return RedirectToAction(nameof(Details), new { id = result.CertificateId.Value });
            }
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// GET: /Student/Certificates/Details/{id}
    /// Displays full certificate details, preview, and verification URL for a specific certificate.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            var cert = await _certificateService.GetCertificateByIdAsync(id, currentUser.Id, isStaff: false);
            if (cert == null) return NotFound();
            return View(cert);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET: /Student/Certificates/Download/{id}
    /// Downloads the official PDF certificate file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        try
        {
            var result = await _certificateService.DownloadCertificatePdfAsync(id, currentUser.Id, isStaff: false);
            if (result == null) return NotFound();

            return File(result.Value.FileBytes, "application/pdf", result.Value.FileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

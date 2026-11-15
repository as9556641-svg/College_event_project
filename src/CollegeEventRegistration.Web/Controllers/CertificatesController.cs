using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Controllers;

/// <summary>
/// Public portal controller for verifying the authenticity and validity of VAP UNIVERSITY event certificates.
/// Accessible by employers, academic institutions, and students without authentication.
/// </summary>
[AllowAnonymous]
public class CertificatesController : Controller
{
    private readonly ICertificateService _certificateService;

    public CertificatesController(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    /// <summary>
    /// GET: /Certificates/Verify/{id?}
    /// Displays the verification portal and verifies a certificate if an identifier or token is provided.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Verify(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return View(null);
        }

        var verificationResult = await _certificateService.VerifyCertificateAsync(id.Trim());
        ViewBag.SearchTerm = id.Trim();
        return View(verificationResult);
    }

    /// <summary>
    /// POST: /Certificates/Verify
    /// Handles public verification searches by certificate number or cryptographic verification token.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifySearch(string searchIdentifier)
    {
        if (string.IsNullOrWhiteSpace(searchIdentifier))
        {
            return RedirectToAction(nameof(Verify));
        }

        return RedirectToAction(nameof(Verify), new { id = searchIdentifier.Trim() });
    }
}

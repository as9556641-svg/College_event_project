using System;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Student Post-Event Feedback &amp; Ratings Controller.
/// Allows verified event attendees to submit 5-star ratings, dimensional evaluations,
/// qualitative reviews, and view their feedback history with strict student tenant isolation.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class FeedbackController : Controller
{
    private readonly IFeedbackService _feedbackService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(
        IFeedbackService feedbackService,
        UserManager<ApplicationUser> userManager,
        ILogger<FeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Feedback
    /// Lists all attended events eligible for feedback as well as submitted evaluations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var eligibleEvents = await _feedbackService.GetStudentFeedbackEligibleEventsAsync(currentUser.Id);
        return View(eligibleEvents);
    }

    /// <summary>
    /// GET: /Student/Feedback/Submit?registrationId={id}
    /// Displays the evaluation form for an attended completed event.
    /// Strictly verifies attendance check-in, conclusion date, and prevents duplicate submissions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Submit(int registrationId)
    {
        if (registrationId <= 0)
        {
            TempData["ErrorMessage"] = "Invalid registration identifier.";
            return RedirectToAction(nameof(Index));
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var eligibility = await _feedbackService.CheckStudentEligibilityAsync(registrationId, currentUser.Id);

        if (eligibility.HasSubmittedFeedback)
        {
            TempData["InfoMessage"] = "You have already submitted feedback for this event.";
            return RedirectToAction(nameof(Details), new { registrationId });
        }

        if (!eligibility.IsEligible)
        {
            TempData["ErrorMessage"] = eligibility.IneligibilityReason ?? "You are not eligible to submit feedback for this registration.";
            return RedirectToAction("Details", "Registrations", new { id = registrationId });
        }

        ViewBag.Eligibility = eligibility;
        var model = new FeedbackSubmissionDto
        {
            RegistrationId = registrationId,
            OverallRating = 5,
            ContentQualityRating = 5,
            SpeakerRating = 5,
            VenueRating = 5
        };

        return View(model);
    }

    /// <summary>
    /// POST: /Student/Feedback/Submit
    /// Persists student ratings and reflections, enforcing anti-forgery, rating boundaries,
    /// and concurrency duplicate prevention.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(FeedbackSubmissionDto model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (model.OverallRating < 1 || model.OverallRating > 5)
        {
            ModelState.AddModelError(nameof(model.OverallRating), "Please select a rating between 1 and 5 stars.");
        }

        if (!string.IsNullOrWhiteSpace(model.Comments) && model.Comments.Length > 1000)
        {
            ModelState.AddModelError(nameof(model.Comments), "Comments cannot exceed 1000 characters.");
        }

        if (!ModelState.IsValid)
        {
            var eligibility = await _feedbackService.CheckStudentEligibilityAsync(model.RegistrationId, currentUser.Id);
            ViewBag.Eligibility = eligibility;
            return View(model);
        }

        var result = await _feedbackService.SubmitFeedbackAsync(model, currentUser.Id);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            var eligibility = await _feedbackService.CheckStudentEligibilityAsync(model.RegistrationId, currentUser.Id);
            ViewBag.Eligibility = eligibility;
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { registrationId = model.RegistrationId });
    }

    /// <summary>
    /// GET: /Student/Feedback/Details?registrationId={id}
    /// Displays submitted feedback details for a specific registration pass.
    /// Strictly verifies student ownership.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int registrationId)
    {
        if (registrationId <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var feedback = await _feedbackService.GetFeedbackByRegistrationIdAsync(
            registrationId,
            currentUser.Id,
            nameof(UserRole.Student));

        if (feedback == null)
        {
            TempData["ErrorMessage"] = "Feedback record not found or access denied.";
            return RedirectToAction(nameof(Index));
        }

        return View(feedback);
    }
}

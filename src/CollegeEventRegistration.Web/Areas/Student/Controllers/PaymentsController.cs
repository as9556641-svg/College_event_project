using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Student payment management controller.
/// Handles secure payment checkout initiation, independent signature verification callbacks,
/// payment receipt generation, and transaction history.
/// Strictly enforces student tenant isolation.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IEventRegistrationService _registrationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IEventRegistrationService registrationService,
        UserManager<ApplicationUser> userManager,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _registrationService = registrationService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Payments
    /// Lists the authenticated student's financial transaction history with status filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] StudentPaymentFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new StudentPaymentFilterCriteria();
        var pagedResult = await _paymentService.GetStudentPaymentHistoryAsync(currentUser.Id, criteria);

        ViewBag.Criteria = criteria;
        return View(pagedResult);
    }

    /// <summary>
    /// GET: /Student/Payments/Checkout?registrationId={id}
    /// Prepares and renders the Razorpay checkout terminal for an unpaid registration.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Checkout(int registrationId)
    {
        if (registrationId <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var regDetails = await _registrationService.GetRegistrationDetailsForStudentAsync(registrationId, currentUser.Id);
        if (regDetails == null)
        {
            TempData["ErrorMessage"] = "Registration not found or unauthorized.";
            return RedirectToAction("Index", "Registrations");
        }

        if (regDetails.EventType == EventType.Free || regDetails.RegistrationFee <= 0)
        {
            TempData["SuccessMessage"] = "This event is completely free. No payment required.";
            return RedirectToAction("Details", "Registrations", new { id = registrationId });
        }

        if (regDetails.Status == RegistrationStatus.Confirmed)
        {
            TempData["SuccessMessage"] = "Your registration is already confirmed!";
            return RedirectToAction("Details", "Registrations", new { id = registrationId });
        }

        // Initialize or retrieve existing pending order
        var orderResult = await _paymentService.InitiatePaymentOrderAsync(registrationId, currentUser.Id);
        if (!orderResult.Success)
        {
            TempData["ErrorMessage"] = orderResult.Message;
            return RedirectToAction("Details", "Registrations", new { id = registrationId });
        }

        ViewBag.Registration = regDetails;
        return View(orderResult);
    }

    /// <summary>
    /// POST: /Student/Payments/Verify
    /// Verifies gateway signature independently, allocates seat, and confirms registration.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify([FromForm] VerifyPaymentRequestDto request)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var verificationResult = await _paymentService.VerifyAndConfirmPaymentAsync(request, currentUser.Id);

        if (verificationResult.Success)
        {
            TempData["SuccessMessage"] = verificationResult.Message;
            if (verificationResult.PaymentId.HasValue)
            {
                return RedirectToAction(nameof(Receipt), new { id = verificationResult.PaymentId.Value });
            }
            return RedirectToAction("Details", "Registrations", new { id = request.RegistrationId });
        }

        TempData["ErrorMessage"] = verificationResult.Message;
        return RedirectToAction(nameof(Checkout), new { registrationId = request.RegistrationId });
    }

    /// <summary>
    /// POST: /Student/Payments/CancelCheckout
    /// Records user cancellation or failure at checkout.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelCheckout(int registrationId, string orderId, string? reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _paymentService.RecordPaymentFailureAsync(
            registrationId,
            orderId,
            null,
            reason ?? "Cancelled by user at checkout",
            currentUser.Id);

        TempData["ErrorMessage"] = "Payment was not completed. You can retry whenever you are ready.";
        return RedirectToAction("Details", "Registrations", new { id = registrationId });
    }

    /// <summary>
    /// GET: /Student/Payments/Receipt/{id}
    /// Official printable receipt for a successful payment transaction.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var payment = await _paymentService.GetPaymentDetailsForStudentAsync(id, currentUser.Id);
        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }
}

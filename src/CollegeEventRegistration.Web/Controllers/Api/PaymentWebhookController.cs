using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Web.Controllers.Api;

/// <summary>
/// Dedicated API controller for receiving and processing asynchronous webhook events from payment gateway.
/// </summary>
[ApiController]
[Route("api/payments")]
[IgnoreAntiforgeryToken]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(
        IPaymentService paymentService,
        ILogger<PaymentWebhookController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// POST: /api/payments/razorpay-webhook
    /// Receives webhook notification, verifies cryptographic signature header,
    /// and idempotently records payment capture and confirms student seat.
    /// </summary>
    [HttpPost("razorpay-webhook")]
    public async Task<IActionResult> RazorpayWebhook()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Rejected webhook call with missing body or X-Razorpay-Signature header.");
            return BadRequest(new { error = "Missing webhook payload or signature." });
        }

        var (success, message) = await _paymentService.ProcessWebhookAsync(rawBody, signature);

        if (!success)
        {
            _logger.LogWarning("Webhook rejection: {Message}", message);
            return BadRequest(new { error = message });
        }

        return Ok(new { status = "success", message });
    }
}

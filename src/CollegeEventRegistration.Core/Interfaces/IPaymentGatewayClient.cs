using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Gateway client abstraction for creating orders, verifying cryptographic signatures,
/// and validating webhook authenticity.
/// </summary>
public interface IPaymentGatewayClient
{
    /// <summary>
    /// Gets the configured gateway Key ID (public key safe for frontend checkout initialization).
    /// </summary>
    string KeyId { get; }

    /// <summary>
    /// Creates a payment order with the payment gateway.
    /// </summary>
    Task<GatewayOrderResult> CreateOrderAsync(
        decimal amount,
        string currency,
        string receipt,
        Dictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cryptographically verifies the gateway signature using HMAC-SHA256 with constant-time equality check.
    /// </summary>
    bool VerifyPaymentSignature(string orderId, string paymentId, string signature);

    /// <summary>
    /// Cryptographically verifies the gateway webhook signature using HMAC-SHA256 with constant-time equality check.
    /// </summary>
    bool VerifyWebhookSignature(string webhookBody, string signature);
}

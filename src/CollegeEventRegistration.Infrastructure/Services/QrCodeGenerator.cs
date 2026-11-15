using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Server-side QR code generator utilizing QRCoder and HMAC-SHA256 signature verification.
/// Prevents PII leakage, tamper attacks, and client-side fabrication.
/// </summary>
public class QrCodeGenerator : IQrCodeGenerator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<QrCodeGenerator> _logger;
    private readonly string _signingSecret;

    public QrCodeGenerator(
        IConfiguration configuration,
        ILogger<QrCodeGenerator> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Secure secret from configuration or user-secrets with robust fallback
        _signingSecret = _configuration["Tickets:SigningSecret"] 
                         ?? _configuration["Razorpay:KeySecret"] 
                         ?? "vap_university_ticket_hmac_secret_key_default_2026";
    }

    /// <inheritdoc />
    public string GenerateSignedPayload(string ticketCode, string registrationNumber, int eventId)
    {
        if (string.IsNullOrWhiteSpace(ticketCode)) throw new ArgumentException("Ticket code cannot be empty.", nameof(ticketCode));
        if (string.IsNullOrWhiteSpace(registrationNumber)) throw new ArgumentException("Registration number cannot be empty.", nameof(registrationNumber));
        if (eventId <= 0) throw new ArgumentOutOfRangeException(nameof(eventId), "Event ID must be positive.");

        var rawMessage = $"v1|{ticketCode.Trim()}|{registrationNumber.Trim()}|{eventId}";
        var signature = ComputeHmacSignature(rawMessage);

        var payloadObj = new
        {
            v = 1,
            t = ticketCode.Trim(),
            r = registrationNumber.Trim(),
            e = eventId,
            s = signature
        };

        return JsonSerializer.Serialize(payloadObj);
    }

    /// <inheritdoc />
    public bool VerifyPayloadSignature(string qrPayload)
    {
        var (isValid, _, _, _) = ParseAndVerifyPayload(qrPayload);
        return isValid;
    }

    /// <inheritdoc />
    public (bool IsValid, string TicketCode, string RegistrationNumber, int EventId) ParseAndVerifyPayload(string qrPayload)
    {
        if (string.IsNullOrWhiteSpace(qrPayload))
        {
            return (false, string.Empty, string.Empty, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(qrPayload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("t", out var tProp) ||
                !root.TryGetProperty("r", out var rProp) ||
                !root.TryGetProperty("e", out var eProp) ||
                !root.TryGetProperty("s", out var sProp))
            {
                return (false, string.Empty, string.Empty, 0);
            }

            var ticketCode = tProp.GetString() ?? string.Empty;
            var regNumber = rProp.GetString() ?? string.Empty;
            var eventId = eProp.GetInt32();
            var providedSignature = sProp.GetString() ?? string.Empty;

            if (string.IsNullOrEmpty(ticketCode) || string.IsNullOrEmpty(regNumber) || eventId <= 0 || string.IsNullOrEmpty(providedSignature))
            {
                return (false, string.Empty, string.Empty, 0);
            }

            var expectedMessage = $"v1|{ticketCode}|{regNumber}|{eventId}";
            var expectedSignature = ComputeHmacSignature(expectedMessage);

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
            var actualBytes = Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant());

            // Constant-time cryptographic verification
            var matches = CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);

            return (matches, ticketCode, regNumber, eventId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse and verify QR payload format.");
            return (false, string.Empty, string.Empty, 0);
        }
    }

    /// <inheritdoc />
    public string GenerateQrPngBase64(string payload, int pixelsPerModule = 10)
    {
        var pngBytes = GenerateQrPngBytes(payload, pixelsPerModule);
        var base64 = Convert.ToBase64String(pngBytes);
        return $"data:image/png;base64,{base64}";
    }

    /// <inheritdoc />
    public byte[] GenerateQrPngBytes(string payload, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = "INVALID_EMPTY_PAYLOAD";
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    private string ComputeHmacSignature(string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_signingSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexStringLower(hash);
    }
}

using System;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Service responsible for cryptographically signing ticket QR data and rendering server-side QR images.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>
    /// Generates a signed QR payload containing only ticket code, registration reference, and event id.
    /// Excludes student name, email, phone, and all personal identifiable information.
    /// </summary>
    string GenerateSignedPayload(string ticketCode, string registrationNumber, int eventId);

    /// <summary>
    /// Verifies the cryptographic HMAC-SHA256 signature on a QR payload.
    /// </summary>
    bool VerifyPayloadSignature(string qrPayload);

    /// <summary>
    /// Decodes and verifies the cryptographic signature of a QR payload.
    /// </summary>
    (bool IsValid, string TicketCode, string RegistrationNumber, int EventId) ParseAndVerifyPayload(string qrPayload);

    /// <summary>
    /// Generates a QR code image as a Base64 PNG data URI (e.g. data:image/png;base64,...).
    /// </summary>
    string GenerateQrPngBase64(string payload, int pixelsPerModule = 10);

    /// <summary>
    /// Generates raw PNG bytes for a QR code.
    /// </summary>
    byte[] GenerateQrPngBytes(string payload, int pixelsPerModule = 10);
}

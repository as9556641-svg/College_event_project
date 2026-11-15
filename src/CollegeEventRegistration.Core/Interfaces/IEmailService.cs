using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Service for dispatching email notifications with safe failure handling.
/// </summary>
public interface IEmailService
{
    Task<EmailDeliveryResult> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    Task<EmailDeliveryResult> QueueEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}

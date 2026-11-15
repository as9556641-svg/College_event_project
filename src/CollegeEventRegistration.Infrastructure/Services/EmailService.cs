using System.Net;
using System.Net.Mail;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CollegeEventRegistration.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> options,
        IEmailQueue emailQueue,
        ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return EmailDeliveryResult.Failed("Recipient email cannot be empty.");
        }

        if (!_settings.IsConfigured)
        {
            _logger.LogWarning(
                "Email dispatch to {Recipient} skipped: SMTP configuration is missing or incomplete (Host: '{Host}'). Application will continue without failure.",
                toEmail, _settings.SmtpHost ?? "null");

            return EmailDeliveryResult.Unavailable(
                "SMTP service is not configured on this environment. Email dispatch skipped safely.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword),
                Timeout = 10000 // 10s timeout to prevent thread blocking
            };

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email successfully dispatched to {Recipient} with subject '{Subject}'", toEmail, subject);
            return EmailDeliveryResult.Sent("Email delivered via SMTP successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}: {Error}", toEmail, ex.Message);
            return EmailDeliveryResult.Failed($"SMTP delivery failure: {ex.Message}");
        }
    }

    public async Task<EmailDeliveryResult> QueueEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return EmailDeliveryResult.Failed("Recipient email cannot be empty.");
        }

        if (!_settings.IsConfigured)
        {
            _logger.LogWarning(
                "Background email queue to {Recipient} skipped: SMTP configuration is missing or incomplete. Application continues safely.",
                toEmail);

            return EmailDeliveryResult.Unavailable("SMTP not configured. Email skipped safely.");
        }

        var message = new EmailMessage
        {
            ToEmail = toEmail,
            Subject = subject,
            BodyHtml = htmlBody,
            CorrelationId = correlationId
        };

        await _emailQueue.QueueBackgroundEmailAsync(message, cancellationToken);
        return new EmailDeliveryResult
        {
            Status = EmailDeliveryStatus.Queued,
            Message = "Email queued for background dispatch."
        };
    }
}

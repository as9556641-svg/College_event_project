using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

public class EmailMessage
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public class EmailDeliveryResult
{
    public EmailDeliveryStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static EmailDeliveryResult Sent(string message = "Email delivered successfully.") =>
        new() { Status = EmailDeliveryStatus.Sent, Message = message };

    public static EmailDeliveryResult Unavailable(string message = "Email service is unconfigured or unavailable; dispatch skipped safely.") =>
        new() { Status = EmailDeliveryStatus.Unavailable, Message = message };

    public static EmailDeliveryResult Failed(string error) =>
        new() { Status = EmailDeliveryStatus.Failed, Message = error };
}

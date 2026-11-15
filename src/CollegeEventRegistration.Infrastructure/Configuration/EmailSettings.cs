namespace CollegeEventRegistration.Infrastructure.Configuration;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = "notifications@vap.edu";
    public string FromName { get; set; } = "VAP UNIVERSITY Event Hub";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        !string.IsNullOrWhiteSpace(SmtpUsername) &&
        !string.IsNullOrWhiteSpace(SmtpPassword);
}

using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Configuration for automated PDF completion and attendance certificate generation.
/// </summary>
public class CertificateTemplate : BaseEntity
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string TemplateTitle { get; set; } = string.Empty;
    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryTitle { get; set; } = string.Empty;
    public string? SignatureImagePath { get; set; }
    public string? BackgroundImagePath { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public string CertificateCodePrefix { get; set; } = "CERT";
}


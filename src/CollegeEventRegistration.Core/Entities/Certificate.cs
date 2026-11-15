using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Issued event completion / participation certificate.
/// </summary>
public class Certificate : BaseEntity
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string CertificateNumber { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;

    public string PdfFilePath { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public string? IssuedByUserId { get; set; }
    public ApplicationUser? IssuedByUser { get; set; }

    public CertificateStatus Status { get; set; } = CertificateStatus.Active;

    public string? RevocationReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }
    public ApplicationUser? RevokedByUser { get; set; }
}

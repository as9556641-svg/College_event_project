using CollegeEventRegistration.Core.Common;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Digital admission pass containing dynamic QR token for event check-in.
/// </summary>
public class Ticket : BaseEntity
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;

    public string TicketCode { get; set; } = string.Empty;
    public string QrCodeData { get; set; } = string.Empty;
    public string? QrCodeImagePath { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
}


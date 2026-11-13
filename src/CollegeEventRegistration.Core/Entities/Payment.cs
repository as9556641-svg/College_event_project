using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Financial transaction record for paid event registrations.
/// </summary>
public class Payment : BaseEntity
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;

    public string? OrderId { get; set; }
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? PaymentMethod { get; set; }

    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

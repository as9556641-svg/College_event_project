using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Student registration for an event or workshop.
/// </summary>
public class EventRegistration : BaseEntity
{
    public string RegistrationNumber { get; set; } = string.Empty;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Confirmed;

    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    // 1-to-Many Navigation for payments & retry history
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Convenience read-only accessors for business logic
    public Payment? SuccessfulPayment => Payments.FirstOrDefault(p => p.Status == PaymentStatus.Success);
    public bool HasSuccessfulPayment => Payments.Any(p => p.Status == PaymentStatus.Success);

    // 1-to-1 Navigations
    public Ticket? Ticket { get; set; }
    public Attendance? Attendance { get; set; }
    public EventFeedback? Feedback { get; set; }
}


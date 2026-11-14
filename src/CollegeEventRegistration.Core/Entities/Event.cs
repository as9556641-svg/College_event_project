using System.ComponentModel.DataAnnotations;
using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// College event or workshop with capacity limits, pricing, and scheduling.
/// </summary>
public class Event : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Classification
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int EventCategoryId { get; set; }
    public EventCategory EventCategory { get; set; } = null!;

    // Pricing & Mode
    public EventType EventType { get; set; } = EventType.Free;
    public decimal RegistrationFee { get; set; } = 0.00m;
    public EventMode EventMode { get; set; } = EventMode.Offline;

    public string? Venue { get; set; }
    public string? OnlineMeetingUrl { get; set; }
    public string? BannerImageUrl { get; set; }

    // Capacity & Concurrency Control
    public int TotalCapacity { get; set; }
    public int RegisteredCount { get; set; } = 0;

    /// <summary>
    /// Concurrency token to prevent race conditions during high-volume seat reservations.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Schedule (Stored in UTC, converted to Asia/Kolkata for display)
    public DateTime RegistrationStartDate { get; set; }
    public DateTime RegistrationEndDate { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    // Lifecycle & Governance
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public bool IsApprovalRequired { get; set; } = true;

    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;

    // Soft delete to protect registration and payment history
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation collections
    public ICollection<EventCoordinator> Coordinators { get; set; } = new List<EventCoordinator>();
    public ICollection<EventSession> Sessions { get; set; } = new List<EventSession>();
    public ICollection<EventEligibility> Eligibilities { get; set; } = new List<EventEligibility>();
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    public ICollection<EventFeedback> FeedbackList { get; set; } = new List<EventFeedback>();
    public CertificateTemplate? CertificateTemplate { get; set; }
}


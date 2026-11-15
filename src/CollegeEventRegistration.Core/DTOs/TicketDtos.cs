using System;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Status of an admission ticket.
/// </summary>
public enum TicketStatus
{
    Valid = 1,
    Used = 2,
    Invalid = 3,
    Cancelled = 4
}

/// <summary>
/// Full projection of an event admission ticket.
/// </summary>
public class TicketDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;

    public string TicketCode { get; set; } = string.Empty;
    public string QrCodeData { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;

    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string? EventSlug { get; set; }
    public string? EventVenue { get; set; }
    public EventMode EventMode { get; set; }
    public EventType EventType { get; set; }
    public decimal RegistrationFee { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public string? EventDepartmentCode { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public string? StudentCollegeName { get; set; }

    public DateTime IssuedAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public bool IsActive { get; set; }
    public TicketStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Validation assessment for a ticket or QR payload.
/// </summary>
public class TicketValidationResultDto
{
    public bool IsValid { get; set; }
    public TicketStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TicketCode { get; set; }
    public int? RegistrationId { get; set; }
    public string? RegistrationNumber { get; set; }
    public int? EventId { get; set; }
    public string? EventTitle { get; set; }
    public string? StudentName { get; set; }
    public TicketDto? Ticket { get; set; }
}

/// <summary>
/// Filter criteria for querying tickets in administration and coordinator views.
/// </summary>
public class TicketFilterCriteria
{
    public int? EventId { get; set; }
    public int? DepartmentId { get; set; }
    public bool? IsUsed { get; set; }
    public bool? IsActive { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

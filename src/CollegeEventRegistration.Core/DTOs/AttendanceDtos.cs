using System;
using System.Collections.Generic;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Status codes representing the outcome of an event check-in operation.
/// </summary>
public enum CheckInStatusCode
{
    Success = 1,
    DuplicateCheckIn = 2,
    TicketNotFound = 3,
    InactiveTicket = 4,
    TicketCancelled = 5,
    PaymentRequired = 6,
    PaymentRefunded = 7,
    InvalidSignature = 8,
    WrongEvent = 9,
    UnauthorizedStaff = 10,
    InvalidPayload = 11,
    RegistrationNotConfirmed = 12,
    EventNotActive = 13,
    ConcurrencyError = 14,
    MissingRequiredData = 15
}

/// <summary>
/// Request DTO for validating and recording event attendance via QR code or manual entry.
/// </summary>
public class CheckInRequestDto
{
    public int EventId { get; set; }
    public string? QrPayload { get; set; }
    public string? TicketCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? RollNumber { get; set; }
    public CheckInMethod CheckInMethod { get; set; } = CheckInMethod.QrCode;
    public string? Remarks { get; set; }
}

/// <summary>
/// Result DTO returned after an attendance check-in attempt.
/// </summary>
public class CheckInResultDto
{
    public bool Success { get; set; }
    public CheckInStatusCode StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? AttendanceId { get; set; }
    public int? RegistrationId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TicketCode { get; set; }
    public int? EventId { get; set; }
    public string? EventTitle { get; set; }
    public string? StudentUserId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentEmail { get; set; }
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public string? CheckedInByUserId { get; set; }
    public string? CheckedInByName { get; set; }
    public CheckInMethod? CheckInMethod { get; set; }
    public string? Remarks { get; set; }
    public int TotalCheckedIn { get; set; }
    public int TotalRegistered { get; set; }
}

/// <summary>
/// Individual attendance record for tables and audit views.
/// </summary>
public class AttendanceRecordDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public string? TicketCode { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string CheckedInByUserId { get; set; } = string.Empty;
    public string CheckedInByName { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.CheckedIn;
    public CheckInMethod CheckInMethod { get; set; } = CheckInMethod.QrCode;
    public string? Remarks { get; set; }
}

/// <summary>
/// High-level metrics summary for an event's attendance check-in progress.
/// </summary>
public class EventAttendanceSummaryDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public string? Venue { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalRegistered { get; set; }
    public int TotalCheckedIn { get; set; }
    public int PendingCheckIn => Math.Max(0, TotalRegistered - TotalCheckedIn);
    public double CheckInPercentage => TotalRegistered > 0
        ? Math.Round((double)TotalCheckedIn / TotalRegistered * 100.0, 1)
        : 0.0;
}

/// <summary>
/// Criteria for filtering and paging attendance records.
/// </summary>
public class AttendanceFilterCriteria
{
    public int? EventId { get; set; }
    public int? DepartmentId { get; set; }
    public CheckInMethod? CheckInMethod { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

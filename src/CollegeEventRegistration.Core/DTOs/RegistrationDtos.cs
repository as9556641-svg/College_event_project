using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Criteria for filtering and searching registrations across Admin and Coordinator portals.
/// </summary>
public class RegistrationFilterCriteria
{
    public int? EventId { get; set; }
    public RegistrationStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>
/// Criteria for filtering a student's own registrations.
/// </summary>
public class StudentRegistrationFilterCriteria
{
    public RegistrationStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public bool OnlyUpcoming { get; set; } = false;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Flattened representation of a registration for tabular administrative and coordinator overviews.
/// </summary>
public class RegistrationSummaryDto
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string? EventDepartmentCode { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public EventMode EventMode { get; set; }
    public EventType EventType { get; set; }
    public decimal RegistrationFee { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public int? StudentYearOfStudy { get; set; }
    public string? StudentCollegeName { get; set; }

    public DateTime RegisteredAt { get; set; }
    public RegistrationStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? TicketCode { get; set; }
}

/// <summary>
/// Comprehensive registration details for standalone inspection and student pass view.
/// </summary>
public class RegistrationDetailDto
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public RegistrationStatus Status { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Event Info
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public EventMode EventMode { get; set; }
    public string? Venue { get; set; }
    public string? OnlineMeetingUrl { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public EventType EventType { get; set; }
    public decimal RegistrationFee { get; set; }

    // Student Info
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public int? StudentYearOfStudy { get; set; }
    public string? StudentCollegeName { get; set; }

    // Ticket & Attendance
    public string? TicketCode { get; set; }
    public bool IsAttended { get; set; }
}

/// <summary>
/// Result returned upon processing a student event registration attempt.
/// </summary>
public class StudentRegistrationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RegistrationId { get; set; }
    public string? RegistrationNumber { get; set; }
    public RegistrationStatus? Status { get; set; }
    public int? EventId { get; set; }
    public string? EventTitle { get; set; }
}

/// <summary>
/// Request payload for administrative or coordinator status transitions.
/// </summary>
public class UpdateRegistrationStatusDto
{
    public int RegistrationId { get; set; }
    public RegistrationStatus NewStatus { get; set; }
    public string? Remarks { get; set; }
}

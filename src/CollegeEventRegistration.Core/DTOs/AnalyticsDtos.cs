using System;
using System.Collections.Generic;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Filter criteria for querying analytics, KPIs, and reports.
/// </summary>
public class AnalyticsFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DepartmentId { get; set; }
    public int? EventCategoryId { get; set; }
    public int? EventId { get; set; }
    public EventStatus? EventStatus { get; set; }
    public string? PeriodPreset { get; set; } // "all", "last7days", "last30days", "last90days", "currentmonth", "currentyear", "custom"
    public string? SearchTerm { get; set; }
}

/// <summary>
/// High-level campus or event KPI metric counters.
/// </summary>
public class DashboardAnalyticsSummaryDto
{
    public EventKpiDto Events { get; set; } = new();
    public RegistrationKpiDto Registrations { get; set; } = new();
    public AttendanceKpiDto Attendance { get; set; } = new();
    public FinancialKpiDto Financials { get; set; } = new();
    public FeedbackKpiDto Feedback { get; set; } = new();
    public CertificateKpiDto Certificates { get; set; } = new();

    // Chart Time Series & Distributions
    public List<TimeSeriesPointDto> MonthlyRegistrationTrend { get; set; } = new();
    public List<DistributionItemDto> EventStatusDistribution { get; set; } = new();
    public List<DistributionItemDto> DepartmentRegistrationDistribution { get; set; } = new();
    public List<DistributionItemDto> AttendanceDistribution { get; set; } = new();
    public List<TimeSeriesPointDto> MonthlyRevenueTrend { get; set; } = new();
    public List<DistributionItemDto> PaymentStatusDistribution { get; set; } = new();
    public List<DistributionItemDto> FeedbackRatingDistribution { get; set; } = new();
    public List<TimeSeriesPointDto> CertificateIssuanceTrend { get; set; } = new();
    public List<TopEventMetricDto> TopEventsByRegistrations { get; set; } = new();
    public List<DepartmentPerformanceReportItemDto> DepartmentPerformance { get; set; } = new();
}

public class EventKpiDto
{
    public int TotalEvents { get; set; }
    public int PublishedEvents { get; set; }
    public int CompletedEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int CancelledEvents { get; set; }
    public int DraftEvents { get; set; }
    public int PendingApprovalEvents { get; set; }
}

public class RegistrationKpiDto
{
    public int TotalRegistrations { get; set; }
    public int ConfirmedRegistrations { get; set; }
    public int PendingRegistrations { get; set; }
    public int WaitlistedRegistrations { get; set; }
    public int CancelledRegistrations { get; set; }
    public double ConversionRate { get; set; } // Confirmed / Total * 100
}

public class AttendanceKpiDto
{
    public int EligibleRegistrations { get; set; } // Strictly Confirmed registrations eligible for attendance
    public int CheckedInCount { get; set; }
    public int NotAttendedCount { get; set; } // EligibleRegistrations - CheckedInCount
    public double AttendancePercentage { get; set; } // CheckedInCount / EligibleRegistrations * 100
}

public class FinancialKpiDto
{
    public decimal GrossCollectedRevenue { get; set; } // Verified Successful payments
    public decimal RefundedAmount { get; set; } // Refunded payments
    public decimal NetCollectedRevenue { get; set; } // Gross - Refunded
    public int SuccessfulTransactionsCount { get; set; }
    public int PendingTransactionsCount { get; set; }
    public int FailedTransactionsCount { get; set; }
    public int RefundedTransactionsCount { get; set; }
    public int TotalTransactionsCount { get; set; }
}

public class FeedbackKpiDto
{
    public int TotalSubmissions { get; set; }
    public double AverageOverallRating { get; set; }
    public double AverageContentRating { get; set; }
    public double AverageSpeakerRating { get; set; }
    public double AverageVenueRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // 1 to 5 stars
    public double ParticipationRate { get; set; } // Submissions / CheckedInCount * 100
}

public class CertificateKpiDto
{
    public int TotalIssued { get; set; }
    public int ActiveCount { get; set; }
    public int RevokedCount { get; set; }
}

public class TimeSeriesPointDto
{
    public string Period { get; set; } = string.Empty; // e.g. "2026-01" or "Jan 2026"
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public int Count { get; set; }
}

public class DistributionItemDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class TopEventMetricDto
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int ConfirmedCount { get; set; }
    public int TotalCapacity { get; set; }
    public double CapacityUtilization { get; set; }
}

/// <summary>
/// Event-scoped analytics model for Coordinators.
/// </summary>
public class CoordinatorAnalyticsDto
{
    public int AssignedEventsCount { get; set; }
    public int PublishedEventsCount { get; set; }
    public int CompletedEventsCount { get; set; }
    public int UpcomingEventsCount { get; set; }
    public int TotalRegistrations { get; set; }
    public int ConfirmedRegistrations { get; set; }
    public int WaitlistedRegistrations { get; set; }
    public int CancelledRegistrations { get; set; }
    public int CheckedInCount { get; set; }
    public int EligibleAttendanceCount { get; set; }
    public double AttendancePercentage { get; set; }
    public decimal VerifiedRevenue { get; set; }
    public int FeedbackSubmissions { get; set; }
    public double AverageFeedbackRating { get; set; }
    public int CertificatesIssued { get; set; }

    public List<EventOptionDto> AssignedEvents { get; set; } = new();
    public List<EventPerformanceReportItemDto> EventPerformance { get; set; } = new();
    public List<TimeSeriesPointDto> RegistrationTrend { get; set; } = new();
    public List<DistributionItemDto> AttendanceDistribution { get; set; } = new();
    public List<DistributionItemDto> FeedbackRatingDistribution { get; set; } = new();
}

public class EventOptionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public DateTime StartDate { get; set; }
}

/// <summary>
/// Performance metrics for a single event in reporting tables and exports.
/// </summary>
public class EventPerformanceReportItemDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EventStatus Status { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalRegistrations { get; set; }
    public int ConfirmedRegistrations { get; set; }
    public int WaitlistedRegistrations { get; set; }
    public int CancelledRegistrations { get; set; }
    public int AttendanceCount { get; set; }
    public double AttendancePercentage { get; set; }
    public decimal VerifiedRevenue { get; set; }
    public int FeedbackCount { get; set; }
    public double AverageRating { get; set; }
    public int CertificatesIssued { get; set; }
}

/// <summary>
/// Performance comparison across academic departments.
/// </summary>
public class DepartmentPerformanceReportItemDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int TotalRegistrations { get; set; }
    public int ConfirmedRegistrations { get; set; }
    public int AttendanceCount { get; set; }
    public double AttendancePercentage { get; set; }
    public decimal VerifiedRevenue { get; set; }
    public double AverageFeedbackRating { get; set; }
    public int CertificatesIssued { get; set; }
}

public class RegistrationReportItemDto
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentRollNumber { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public RegistrationStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal Amount { get; set; }
}

public class AttendanceReportItemDto
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentRollNumber { get; set; } = string.Empty;
    public DateTime? CheckedInAt { get; set; }
    public CheckInMethod? CheckInMethod { get; set; }
    public string? CheckedInByName { get; set; }
    public string? Remarks { get; set; }
    public AttendanceStatus Status { get; set; }
}

public class FinancialReportItemDto
{
    public string? TransactionId { get; set; }
    public string? OrderId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class FeedbackSummaryReportItemDto
{
    public int FeedbackId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty; // Masked if anonymous
    public string RollNumber { get; set; } = string.Empty; // Masked if anonymous
    public bool IsAnonymous { get; set; }
    public int OverallRating { get; set; }
    public int ContentQualityRating { get; set; }
    public int SpeakerRating { get; set; }
    public int VenueRating { get; set; }
    public string? Comments { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class CertificateReportItemDto
{
    public string CertificateNumber { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string RollNumber { get; set; } = string.Empty;
    public CertificateStatus Status { get; set; }
    public DateTime IssuedAt { get; set; }
    public string? IssuedByName { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}

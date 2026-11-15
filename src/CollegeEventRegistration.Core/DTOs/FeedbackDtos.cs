using System;
using System.Collections.Generic;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Status codes representing the outcome of a feedback operation.
/// </summary>
public enum FeedbackStatusCode
{
    Success = 1,
    RegistrationNotFound = 2,
    UnauthorizedStudent = 3,
    RegistrationNotConfirmed = 4,
    AttendanceRequired = 5,
    EventNotEnded = 6,
    DuplicateSubmission = 7,
    InvalidRating = 8,
    CommentTooLong = 9,
    UnauthorizedStaff = 10,
    ConcurrencyError = 11,
    EventNotFound = 12
}

/// <summary>
/// Request payload submitted by an authenticated student evaluating an attended event.
/// </summary>
public class FeedbackSubmissionDto
{
    public int RegistrationId { get; set; }

    /// <summary>
    /// Overall satisfaction rating (1 to 5 stars).
    /// </summary>
    public int OverallRating { get; set; }

    /// <summary>
    /// Evaluation of session content quality and depth (1 to 5 stars).
    /// </summary>
    public int ContentQualityRating { get; set; }

    /// <summary>
    /// Evaluation of instructor, speaker, or presenter effectiveness (1 to 5 stars).
    /// </summary>
    public int SpeakerRating { get; set; }

    /// <summary>
    /// Evaluation of the venue, facilities, or online platform (1 to 5 stars).
    /// </summary>
    public int VenueRating { get; set; }

    /// <summary>
    /// Optional qualitative feedback, reflections, and suggestions (maximum 1000 characters).
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// Flag indicating whether the student wishes their name and roll number to be masked from public/staff displays.
    /// </summary>
    public bool IsAnonymous { get; set; } = false;
}

/// <summary>
/// Result returned after attempting to persist a feedback submission.
/// </summary>
public class FeedbackResultDto
{
    public bool Success { get; set; }
    public FeedbackStatusCode StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? FeedbackId { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public int OverallRating { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>
/// Detailed evaluation record for display in student history, coordinator reviews, and admin audits.
/// </summary>
public class FeedbackItemDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;

    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string? EventCategoryName { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }

    public int OverallRating { get; set; }
    public int ContentQualityRating { get; set; }
    public int SpeakerRating { get; set; }
    public int VenueRating { get; set; }
    public string? Comments { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime SubmittedAt { get; set; }
}

/// <summary>
/// Aggregated rating metrics, satisfaction averages, and distribution for an event.
/// </summary>
public class EventFeedbackSummaryDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public int TotalFeedbacks { get; set; }
    public int TotalAttendees { get; set; }
    public double FeedbackSubmissionRate { get; set; }
    public double AverageOverallRating { get; set; }
    public double AverageContentRating { get; set; }
    public double AverageSpeakerRating { get; set; }
    public double AverageVenueRating { get; set; }

    /// <summary>
    /// Rating counts keyed by star score (1 through 5).
    /// </summary>
    public Dictionary<int, int> RatingDistribution { get; set; } = new()
    {
        { 1, 0 },
        { 2, 0 },
        { 3, 0 },
        { 4, 0 },
        { 5, 0 }
    };

    public List<FeedbackItemDto> RecentFeedbacks { get; set; } = new();
}

/// <summary>
/// Multi-criteria search and filter parameters for coordinator and administrator feedback ledgers.
/// </summary>
public class FeedbackFilterCriteria
{
    public int? EventId { get; set; }
    public int? DepartmentId { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>
/// Eligibility status for a student considering submitting feedback for an event registration.
/// </summary>
public class StudentFeedbackEligibilityDto
{
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    public bool IsEligible { get; set; }
    public string? IneligibilityReason { get; set; }

    public bool HasAttended { get; set; }
    public bool HasEventEnded { get; set; }
    public bool HasSubmittedFeedback { get; set; }

    public int? ExistingFeedbackId { get; set; }
    public int? ExistingOverallRating { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

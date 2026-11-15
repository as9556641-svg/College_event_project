using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing Phase 11: Feedback &amp; Ratings.
/// Enforces post-event evaluation eligibility, verified attendance requirements,
/// rating range limits, anti-forgery and duplicate prevention,
/// coordinator event-level scoping, and aggregated rating metrics.
/// </summary>
public class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(
        ApplicationDbContext context,
        IAuditLogService auditLogService,
        ILogger<FeedbackService> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FeedbackResultDto> SubmitFeedbackAsync(
        FeedbackSubmissionDto dto,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        // 1. Authenticated User Guard
        if (string.IsNullOrWhiteSpace(studentUserId))
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.UnauthorizedStudent,
                Message = "Authentication is required to submit feedback."
            };
        }

        // 2. Rating Range Validation (strictly 1 to 5)
        if (dto.OverallRating < 1 || dto.OverallRating > 5)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.InvalidRating,
                Message = "Overall rating must be between 1 and 5 stars.",
                RegistrationId = dto.RegistrationId
            };
        }

        // Normalize dimensional ratings: if <= 0 or > 5, default to overall rating
        var contentRating = (dto.ContentQualityRating >= 1 && dto.ContentQualityRating <= 5)
            ? dto.ContentQualityRating
            : dto.OverallRating;

        var speakerRating = (dto.SpeakerRating >= 1 && dto.SpeakerRating <= 5)
            ? dto.SpeakerRating
            : dto.OverallRating;

        var venueRating = (dto.VenueRating >= 1 && dto.VenueRating <= 5)
            ? dto.VenueRating
            : dto.OverallRating;

        // 3. Comments Validation (Max 1000 characters)
        string? cleanComments = null;
        if (!string.IsNullOrWhiteSpace(dto.Comments))
        {
            var trimmed = dto.Comments.Trim();
            if (trimmed.Length > 1000)
            {
                return new FeedbackResultDto
                {
                    Success = false,
                    StatusCode = FeedbackStatusCode.CommentTooLong,
                    Message = "Feedback comments cannot exceed 1000 characters.",
                    RegistrationId = dto.RegistrationId
                };
            }
            cleanComments = trimmed;
        }

        // 4. Load Registration with Event, Attendance, and existing Feedback
        var registration = await _context.EventRegistrations
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Include(r => r.Feedback)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == dto.RegistrationId, cancellationToken);

        if (registration == null)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.RegistrationNotFound,
                Message = "Registration record not found.",
                RegistrationId = dto.RegistrationId
            };
        }

        // 5. Student Tenant Isolation Guard
        if (registration.UserId != studentUserId)
        {
            _logger.LogWarning("Security Violation: User {AttemptedUserId} attempted to submit feedback for Registration #{RegId} owned by User {OwnerId}",
                studentUserId, dto.RegistrationId, registration.UserId);

            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.UnauthorizedStudent,
                Message = "Access denied: you can only submit feedback for your own registrations.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 6. Registration Status Guard (Must be Confirmed)
        if (registration.Status != RegistrationStatus.Confirmed)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.RegistrationNotConfirmed,
                Message = $"Feedback cannot be submitted because this registration is {registration.Status}.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 7. Verified Attendance Guard (Student must have checked in)
        if (registration.Attendance == null || registration.Attendance.Status != AttendanceStatus.CheckedIn)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.AttendanceRequired,
                Message = "Feedback can only be submitted by participants with a verified event attendance check-in.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 8. Post-Event Requirement Guard (Event must have concluded or marked Completed)
        var utcNow = DateTime.UtcNow;
        var hasEventEnded = registration.Event.EventEndDate <= utcNow || registration.Event.Status == EventStatus.Completed;
        if (!hasEventEnded)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.EventNotEnded,
                Message = $"Feedback cannot be submitted before the event has concluded (End Date: {registration.Event.EventEndDate:MMM dd, yyyy HH:mm} UTC).",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 9. Duplicate Feedback Guard
        if (registration.Feedback != null || await _context.EventFeedbacks.AnyAsync(f => f.RegistrationId == dto.RegistrationId, cancellationToken))
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.DuplicateSubmission,
                Message = "Feedback has already been submitted for this event registration.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 10. Atomic Persistence under Execution Strategy with Unique Constraint Protection
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        var feedback = new EventFeedback
        {
            RegistrationId = registration.Id,
            EventId = registration.EventId,
            UserId = studentUserId,
            OverallRating = dto.OverallRating,
            ContentQualityRating = contentRating,
            SpeakerRating = speakerRating,
            VenueRating = venueRating,
            Comments = cleanComments,
            IsAnonymous = dto.IsAnonymous,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        try
        {
            await executionStrategy.ExecuteAsync(async () =>
            {
                // Double check inside transaction boundary
                var exists = await _context.EventFeedbacks
                    .AnyAsync(f => f.RegistrationId == registration.Id, cancellationToken);

                if (exists)
                {
                    throw new InvalidOperationException("Duplicate feedback submission detected.");
                }

                _context.EventFeedbacks.Add(feedback);
                await _context.SaveChangesAsync(cancellationToken);
            });
        }
        catch (InvalidOperationException)
        {
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.DuplicateSubmission,
                Message = "Feedback has already been submitted for this registration.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DbUpdateException while persisting feedback for registration #{RegistrationId}", dto.RegistrationId);
            return new FeedbackResultDto
            {
                Success = false,
                StatusCode = FeedbackStatusCode.ConcurrencyError,
                Message = "A duplicate submission was detected or a concurrency collision occurred. Please refresh.",
                RegistrationId = dto.RegistrationId,
                EventId = registration.EventId
            };
        }

        // 11. Audit Logging
        await _auditLogService.LogAsync(
            action: "SubmitEventFeedback",
            entityName: nameof(EventFeedback),
            entityId: feedback.Id.ToString(),
            userId: studentUserId,
            oldValues: null,
            newValues: $"Rating: {feedback.OverallRating}/5, EventId: {feedback.EventId}, Anonymous: {feedback.IsAnonymous}");

        _logger.LogInformation("Feedback #{FeedbackId} successfully submitted by student {UserId} for Event #{EventId} with rating {Rating}/5",
            feedback.Id, studentUserId, feedback.EventId, feedback.OverallRating);

        return new FeedbackResultDto
        {
            Success = true,
            StatusCode = FeedbackStatusCode.Success,
            Message = "Thank you! Your feedback and ratings have been submitted successfully.",
            FeedbackId = feedback.Id,
            RegistrationId = registration.Id,
            EventId = registration.EventId,
            OverallRating = feedback.OverallRating,
            SubmittedAt = feedback.SubmittedAt
        };
    }

    /// <inheritdoc />
    public async Task<StudentFeedbackEligibilityDto> CheckStudentEligibilityAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0 || string.IsNullOrWhiteSpace(studentUserId))
        {
            return new StudentFeedbackEligibilityDto
            {
                RegistrationId = registrationId,
                IsEligible = false,
                IneligibilityReason = "Invalid registration or unauthenticated student."
            };
        }

        var registration = await _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Include(r => r.Feedback)
            .FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);

        if (registration == null || registration.UserId != studentUserId)
        {
            return new StudentFeedbackEligibilityDto
            {
                RegistrationId = registrationId,
                IsEligible = false,
                IneligibilityReason = "Registration record not found or access denied."
            };
        }

        var utcNow = DateTime.UtcNow;
        var hasAttended = registration.Attendance != null && registration.Attendance.Status == AttendanceStatus.CheckedIn;
        var hasEventEnded = registration.Event.EventEndDate <= utcNow || registration.Event.Status == EventStatus.Completed;
        var hasSubmitted = registration.Feedback != null;

        var result = new StudentFeedbackEligibilityDto
        {
            RegistrationId = registration.Id,
            RegistrationNumber = registration.RegistrationNumber,
            EventId = registration.EventId,
            EventTitle = registration.Event.Title,
            EventStartDate = registration.Event.EventStartDate,
            EventEndDate = registration.Event.EventEndDate,
            HasAttended = hasAttended,
            HasEventEnded = hasEventEnded,
            HasSubmittedFeedback = hasSubmitted,
            ExistingFeedbackId = registration.Feedback?.Id,
            ExistingOverallRating = registration.Feedback?.OverallRating,
            SubmittedAt = registration.Feedback?.SubmittedAt
        };

        if (hasSubmitted)
        {
            result.IsEligible = false;
            result.IneligibilityReason = "Feedback has already been submitted for this registration.";
        }
        else if (registration.Status != RegistrationStatus.Confirmed)
        {
            result.IsEligible = false;
            result.IneligibilityReason = $"Registration status is {registration.Status}. Only confirmed registrations may leave feedback.";
        }
        else if (!hasAttended)
        {
            result.IsEligible = false;
            result.IneligibilityReason = "Only attendees with verified venue check-in are eligible to submit feedback.";
        }
        else if (!hasEventEnded)
        {
            result.IsEligible = false;
            result.IneligibilityReason = $"Feedback opens after the event concludes on {registration.Event.EventEndDate:MMM dd, yyyy HH:mm} UTC.";
        }
        else
        {
            result.IsEligible = true;
            result.IneligibilityReason = null;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentFeedbackEligibilityDto>> GetStudentFeedbackEligibleEventsAsync(
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentUserId))
        {
            return Array.Empty<StudentFeedbackEligibilityDto>();
        }

        var registrations = await _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Include(r => r.Feedback)
            .Where(r => r.UserId == studentUserId && r.Status == RegistrationStatus.Confirmed)
            .OrderByDescending(r => r.Event.EventEndDate)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var list = new List<StudentFeedbackEligibilityDto>(registrations.Count);

        foreach (var reg in registrations)
        {
            var hasAttended = reg.Attendance != null && reg.Attendance.Status == AttendanceStatus.CheckedIn;
            var hasEventEnded = reg.Event.EventEndDate <= utcNow || reg.Event.Status == EventStatus.Completed;
            var hasSubmitted = reg.Feedback != null;

            var dto = new StudentFeedbackEligibilityDto
            {
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                EventStartDate = reg.Event.EventStartDate,
                EventEndDate = reg.Event.EventEndDate,
                HasAttended = hasAttended,
                HasEventEnded = hasEventEnded,
                HasSubmittedFeedback = hasSubmitted,
                ExistingFeedbackId = reg.Feedback?.Id,
                ExistingOverallRating = reg.Feedback?.OverallRating,
                SubmittedAt = reg.Feedback?.SubmittedAt
            };

            if (hasSubmitted)
            {
                dto.IsEligible = false;
                dto.IneligibilityReason = "Feedback submitted.";
            }
            else if (!hasAttended)
            {
                dto.IsEligible = false;
                dto.IneligibilityReason = "Attendance check-in not verified.";
            }
            else if (!hasEventEnded)
            {
                dto.IsEligible = false;
                dto.IneligibilityReason = "Event has not yet ended.";
            }
            else
            {
                dto.IsEligible = true;
                dto.IneligibilityReason = null;
            }

            list.Add(dto);
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<FeedbackItemDto?> GetFeedbackByRegistrationIdAsync(
        int registrationId,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0) return null;

        var feedback = await _context.EventFeedbacks
            .AsNoTracking()
            .Include(f => f.Registration)
            .Include(f => f.Event)
                .ThenInclude(e => e.Department)
            .Include(f => f.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.RegistrationId == registrationId, cancellationToken);

        if (feedback == null) return null;

        // Authorization Guard
        if (userRole == nameof(UserRole.Student))
        {
            if (feedback.UserId != userId)
            {
                _logger.LogWarning("Access denied: Student {UserId} attempted to access Feedback for Registration #{RegId}", userId, registrationId);
                return null;
            }
        }
        else if (userRole == nameof(UserRole.Coordinator))
        {
            var canAccess = await CanStaffAccessEventFeedbackAsync(feedback.EventId, userId, userRole, cancellationToken);
            if (!canAccess)
            {
                _logger.LogWarning("Access denied: Coordinator {UserId} attempted to access Feedback for unassigned Event #{EventId}", userId, feedback.EventId);
                return null;
            }
        }

        return MapToItemDto(feedback, userRole);
    }

    /// <inheritdoc />
    public async Task<FeedbackItemDto?> GetFeedbackByIdAsync(
        int feedbackId,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        if (feedbackId <= 0) return null;

        var feedback = await _context.EventFeedbacks
            .AsNoTracking()
            .Include(f => f.Registration)
            .Include(f => f.Event)
                .ThenInclude(e => e.Department)
            .Include(f => f.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == feedbackId, cancellationToken);

        if (feedback == null) return null;

        // Authorization Guard
        if (userRole == nameof(UserRole.Student))
        {
            if (feedback.UserId != userId)
            {
                return null;
            }
        }
        else if (userRole == nameof(UserRole.Coordinator))
        {
            var canAccess = await CanStaffAccessEventFeedbackAsync(feedback.EventId, userId, userRole, cancellationToken);
            if (!canAccess)
            {
                return null;
            }
        }

        return MapToItemDto(feedback, userRole);
    }

    /// <inheritdoc />
    public async Task<EventFeedbackSummaryDto> GetEventFeedbackSummaryAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            throw new ArgumentException("Valid eventId is required.", nameof(eventId));
        }

        // Coordinator Event-Scoping Authorization Guard
        var canAccess = await CanStaffAccessEventFeedbackAsync(eventId, staffUserId, staffRole, cancellationToken);
        if (!canAccess)
        {
            _logger.LogWarning("Unauthorized access attempt by {UserId} (Role: {Role}) for Event #{EventId} feedback summary",
                staffUserId, staffRole, eventId);
            throw new UnauthorizedAccessException($"Staff member is not authorized to access feedback for Event #{eventId}.");
        }

        var ev = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (ev == null)
        {
            throw new KeyNotFoundException($"Event #{eventId} not found.");
        }

        var feedbacks = await _context.EventFeedbacks
            .AsNoTracking()
            .Include(f => f.Registration)
            .Include(f => f.User)
            .Where(f => f.EventId == eventId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        var totalAttendees = await _context.Attendances
            .AsNoTracking()
            .CountAsync(a => a.Registration.EventId == eventId && a.Status == AttendanceStatus.CheckedIn, cancellationToken);

        var totalFeedbacks = feedbacks.Count;
        var summary = new EventFeedbackSummaryDto
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            TotalFeedbacks = totalFeedbacks,
            TotalAttendees = totalAttendees,
            FeedbackSubmissionRate = totalAttendees > 0 ? Math.Round((double)totalFeedbacks / totalAttendees * 100, 1) : 0.0,
            AverageOverallRating = totalFeedbacks > 0 ? Math.Round(feedbacks.Average(f => f.OverallRating), 2) : 0.0,
            AverageContentRating = totalFeedbacks > 0 ? Math.Round(feedbacks.Average(f => f.ContentQualityRating), 2) : 0.0,
            AverageSpeakerRating = totalFeedbacks > 0 ? Math.Round(feedbacks.Average(f => f.SpeakerRating), 2) : 0.0,
            AverageVenueRating = totalFeedbacks > 0 ? Math.Round(feedbacks.Average(f => f.VenueRating), 2) : 0.0
        };

        // Populate rating distribution (1 to 5 stars)
        for (int star = 1; star <= 5; star++)
        {
            summary.RatingDistribution[star] = feedbacks.Count(f => f.OverallRating == star);
        }

        // Recent feedback items (up to 10)
        summary.RecentFeedbacks = feedbacks.Take(10).Select(f => MapToItemDto(f, staffRole)).ToList();

        return summary;
    }

    /// <inheritdoc />
    public async Task<PagedResult<FeedbackItemDto>> GetFeedbackRecordsAsync(
        FeedbackFilterCriteria criteria,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new FeedbackFilterCriteria();

        // 1. Coordinator Event-Scoping Guard
        if (staffRole == nameof(UserRole.Coordinator))
        {
            if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
            {
                var canManage = await CanStaffAccessEventFeedbackAsync(criteria.EventId.Value, staffUserId, staffRole, cancellationToken);
                if (!canManage)
                {
                    _logger.LogWarning("Coordinator {UserId} denied access to feedback for unassigned Event #{EventId}",
                        staffUserId, criteria.EventId.Value);
                    throw new UnauthorizedAccessException($"Coordinator is not authorized for Event #{criteria.EventId.Value}.");
                }
            }
        }

        var query = _context.EventFeedbacks
            .AsNoTracking()
            .Include(f => f.Registration)
            .Include(f => f.Event)
                .ThenInclude(e => e.Department)
            .Include(f => f.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(f => f.User)
            .AsQueryable();

        // If Coordinator without specific event filter, limit to all their assigned events
        if (staffRole == nameof(UserRole.Coordinator))
        {
            if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
            {
                query = query.Where(f => f.EventId == criteria.EventId.Value);
            }
            else
            {
                query = query.Where(f => !f.Event.IsDeleted &&
                    (f.Event.CreatedByUserId == staffUserId || f.Event.Coordinators.Any(c => c.UserId == staffUserId)));
            }
        }
        else if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(f => f.EventId == criteria.EventId.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(f => f.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.MinRating.HasValue && criteria.MinRating.Value > 0)
        {
            query = query.Where(f => f.OverallRating >= criteria.MinRating.Value);
        }

        if (criteria.MaxRating.HasValue && criteria.MaxRating.Value > 0)
        {
            query = query.Where(f => f.OverallRating <= criteria.MaxRating.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim();
            query = query.Where(f =>
                f.Event.Title.Contains(term) ||
                (f.Comments != null && f.Comments.Contains(term)) ||
                (!f.IsAnonymous && (
                    f.User.FirstName.Contains(term) ||
                    f.User.LastName.Contains(term) ||
                    (f.User.RollNumber != null && f.User.RollNumber.Contains(term))
                )));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => MapToItemDto(f, staffRole))
            .ToListAsync(cancellationToken);

        return new PagedResult<FeedbackItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool> CanStaffAccessEventFeedbackAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0 || string.IsNullOrWhiteSpace(staffUserId))
        {
            return false;
        }

        if (staffRole == nameof(UserRole.Admin) || staffRole == nameof(UserRole.SuperAdmin))
        {
            return true;
        }

        if (staffRole == nameof(UserRole.Coordinator))
        {
            return await _context.Events
                .AnyAsync(e => e.Id == eventId && !e.IsDeleted &&
                    (e.CreatedByUserId == staffUserId || e.Coordinators.Any(c => c.UserId == staffUserId)),
                    cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Helper method to project an EventFeedback entity to a FeedbackItemDto,
    /// respecting anonymity privacy rules.
    /// </summary>
    private static FeedbackItemDto MapToItemDto(EventFeedback f, string viewerRole)
    {
        // Anonymity rule: If student opted for anonymity, mask name and roll number unless viewed by SuperAdmin/Admin
        var isPrivilegedAdmin = viewerRole == nameof(UserRole.Admin) || viewerRole == nameof(UserRole.SuperAdmin);
        var isAnonymous = f.IsAnonymous;

        string studentName;
        string? studentRoll;
        string studentEmail;

        if (isAnonymous && !isPrivilegedAdmin)
        {
            studentName = "Anonymous Attendee";
            studentRoll = "Masked";
            studentEmail = "masked@anonymous.student";
        }
        else
        {
            studentName = f.User != null ? $"{f.User.FirstName} {f.User.LastName}".Trim() : "Participant";
            studentRoll = f.User?.RollNumber;
            studentEmail = f.User?.Email ?? string.Empty;
        }

        return new FeedbackItemDto
        {
            Id = f.Id,
            RegistrationId = f.RegistrationId,
            RegistrationNumber = f.Registration?.RegistrationNumber ?? string.Empty,
            EventId = f.EventId,
            EventTitle = f.Event?.Title ?? string.Empty,
            EventCategoryName = f.Event?.EventCategory?.Name,
            DepartmentName = f.Event?.Department?.Name,
            EventStartDate = f.Event?.EventStartDate ?? DateTime.MinValue,
            EventEndDate = f.Event?.EventEndDate ?? DateTime.MinValue,
            StudentUserId = f.UserId,
            StudentName = studentName,
            StudentEmail = studentEmail,
            StudentRollNumber = studentRoll,
            StudentDepartment = f.User?.Department,
            OverallRating = f.OverallRating,
            ContentQualityRating = f.ContentQualityRating,
            SpeakerRating = f.SpeakerRating,
            VenueRating = f.VenueRating,
            Comments = f.Comments,
            IsAnonymous = f.IsAnonymous,
            SubmittedAt = f.SubmittedAt
        };
    }
}

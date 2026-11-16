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
/// Domain service for aggregated analytics, KPIs, trends, and reports.
/// Adheres strictly to server-side EF Core projection and grouping to prevent memory dumps and N+1 queries.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(ApplicationDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DashboardAnalyticsSummaryDto> GetCampusAnalyticsSummaryAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveDateWindow(filter);

        // Base Queryables
        var eventsQuery = BuildFilteredEventsQuery(filter, start, end, null);
        var regQuery = BuildFilteredRegistrationsQuery(filter, start, end, null);
        var paymentQuery = BuildFilteredPaymentsQuery(filter, start, end, null);
        var feedbackQuery = BuildFilteredFeedbackQuery(filter, start, end, null);
        var certQuery = BuildFilteredCertificatesQuery(filter, start, end, null);

        var now = DateTime.UtcNow;

        // 1. Events KPI
        var totalEvents = await eventsQuery.CountAsync(cancellationToken);
        var publishedEvents = await eventsQuery.CountAsync(e => e.Status == EventStatus.Published, cancellationToken);
        var completedEvents = await eventsQuery.CountAsync(e => e.Status == EventStatus.Completed || e.EventEndDate < now, cancellationToken);
        var upcomingEvents = await eventsQuery.CountAsync(e => e.Status != EventStatus.Cancelled && e.EventStartDate > now, cancellationToken);
        var cancelledEvents = await eventsQuery.CountAsync(e => e.Status == EventStatus.Cancelled, cancellationToken);
        var draftEvents = await eventsQuery.CountAsync(e => e.Status == EventStatus.Draft, cancellationToken);
        var pendingApprovalEvents = await eventsQuery.CountAsync(e => e.Status == EventStatus.PendingApproval, cancellationToken);

        // 2. Registrations KPI
        var totalRegs = await regQuery.CountAsync(cancellationToken);
        var confirmedRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Confirmed, cancellationToken);
        var pendingRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Pending, cancellationToken);
        var waitlistedRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Waitlisted, cancellationToken);
        var cancelledRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Cancelled, cancellationToken);
        var conversionRate = totalRegs > 0 ? Math.Round(confirmedRegs * 100.0 / totalRegs, 2) : 0.0;

        // 3. Attendance KPI (Only Confirmed registrations are eligible)
        var eligibleAttendance = confirmedRegs;
        var checkedInCount = await regQuery
            .Where(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn)
            .CountAsync(cancellationToken);
        var notAttendedCount = Math.Max(0, eligibleAttendance - checkedInCount);
        var attendancePercentage = eligibleAttendance > 0 ? Math.Round(checkedInCount * 100.0 / eligibleAttendance, 2) : 0.0;

        // 4. Financials KPI (Strictly verified payments)
        var grossRevenue = await paymentQuery
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var refundedAmount = await paymentQuery
            .Where(p => p.Status == PaymentStatus.Refunded)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        var netRevenue = grossRevenue - refundedAmount;

        var successTxCount = await paymentQuery.CountAsync(p => p.Status == PaymentStatus.Success, cancellationToken);
        var pendingTxCount = await paymentQuery.CountAsync(p => p.Status == PaymentStatus.Pending, cancellationToken);
        var failedTxCount = await paymentQuery.CountAsync(p => p.Status == PaymentStatus.Failed, cancellationToken);
        var refundedTxCount = await paymentQuery.CountAsync(p => p.Status == PaymentStatus.Refunded, cancellationToken);
        var totalTxCount = await paymentQuery.CountAsync(cancellationToken);

        // 5. Feedback KPI
        var totalFeedback = await feedbackQuery.CountAsync(cancellationToken);
        var avgOverall = totalFeedback > 0 ? Math.Round(await feedbackQuery.AverageAsync(f => (double)f.OverallRating, cancellationToken), 2) : 0.0;
        var avgContent = totalFeedback > 0 ? Math.Round(await feedbackQuery.AverageAsync(f => (double)f.ContentQualityRating, cancellationToken), 2) : 0.0;
        var avgSpeaker = totalFeedback > 0 ? Math.Round(await feedbackQuery.AverageAsync(f => (double)f.SpeakerRating, cancellationToken), 2) : 0.0;
        var avgVenue = totalFeedback > 0 ? Math.Round(await feedbackQuery.AverageAsync(f => (double)f.VenueRating, cancellationToken), 2) : 0.0;
        var feedbackParticipation = checkedInCount > 0 ? Math.Round(totalFeedback * 100.0 / checkedInCount, 2) : 0.0;

        var ratingDistRaw = await feedbackQuery
            .GroupBy(f => f.OverallRating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ratingDict = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        foreach (var r in ratingDistRaw)
        {
            if (r.Rating >= 1 && r.Rating <= 5)
            {
                ratingDict[r.Rating] = r.Count;
            }
        }

        // 6. Certificates KPI
        var totalCerts = await certQuery.CountAsync(cancellationToken);
        var activeCerts = await certQuery.CountAsync(c => c.Status == CertificateStatus.Active, cancellationToken);
        var revokedCerts = await certQuery.CountAsync(c => c.Status == CertificateStatus.Revoked, cancellationToken);

        // 7. Time Series & Distributions
        // Monthly Registration Trend (Group by Year-Month)
        var monthlyRegsRaw = await regQuery
            .GroupBy(r => new { r.RegisteredAt.Year, r.RegisteredAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var monthlyRegs = monthlyRegsRaw.Select(x => new TimeSeriesPointDto
        {
            Period = $"{x.Year:D4}-{x.Month:D2}",
            Label = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"),
            Count = x.Count,
            Value = x.Count
        }).ToList();

        // Monthly Revenue Trend (Successful payments only)
        var monthlyRevRaw = await paymentQuery
            .Where(p => p.Status == PaymentStatus.Success && p.CompletedAt.HasValue)
            .GroupBy(p => new { p.CompletedAt!.Value.Year, p.CompletedAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount), Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var monthlyRevenue = monthlyRevRaw.Select(x => new TimeSeriesPointDto
        {
            Period = $"{x.Year:D4}-{x.Month:D2}",
            Label = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"),
            Value = (double)x.Total,
            Count = x.Count
        }).ToList();

        // Event Status Distribution
        var eventStatusesRaw = await eventsQuery
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var eventStatusDist = eventStatusesRaw.Select(x => new DistributionItemDto
        {
            Category = x.Status.ToString(),
            Count = x.Count,
            Percentage = totalEvents > 0 ? Math.Round(x.Count * 100.0 / totalEvents, 1) : 0.0
        }).ToList();

        // Department-wise Registrations
        var deptRegsRaw = await regQuery
            .GroupBy(r => r.Event.Department.Name)
            .Select(g => new { Department = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var deptRegsDist = deptRegsRaw.Select(x => new DistributionItemDto
        {
            Category = x.Department,
            Count = x.Count,
            Percentage = totalRegs > 0 ? Math.Round(x.Count * 100.0 / totalRegs, 1) : 0.0
        }).ToList();

        // Attendance vs Non-Attendance
        var attendanceDist = new List<DistributionItemDto>
        {
            new() { Category = "Checked In", Count = checkedInCount, Percentage = attendancePercentage },
            new() { Category = "Not Attended", Count = notAttendedCount, Percentage = eligibleAttendance > 0 ? Math.Round(notAttendedCount * 100.0 / eligibleAttendance, 1) : 0.0 }
        };

        // Payment Status Distribution
        var payStatusRaw = await paymentQuery
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var payStatusDist = payStatusRaw.Select(x => new DistributionItemDto
        {
            Category = x.Status.ToString(),
            Count = x.Count,
            Percentage = totalTxCount > 0 ? Math.Round(x.Count * 100.0 / totalTxCount, 1) : 0.0
        }).ToList();

        // Feedback Rating Distribution
        var feedbackDist = ratingDict.OrderBy(kv => kv.Key).Select(kv => new DistributionItemDto
        {
            Category = $"{kv.Key} Star{(kv.Key > 1 ? "s" : "")}",
            Count = kv.Value,
            Percentage = totalFeedback > 0 ? Math.Round(kv.Value * 100.0 / totalFeedback, 1) : 0.0
        }).ToList();

        // Certificate Trend
        var certTrendRaw = await certQuery
            .GroupBy(c => new { c.IssuedAt.Year, c.IssuedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var certTrend = certTrendRaw.Select(x => new TimeSeriesPointDto
        {
            Period = $"{x.Year:D4}-{x.Month:D2}",
            Label = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"),
            Count = x.Count,
            Value = x.Count
        }).ToList();

        // Top Events by Confirmed Registrations
        var topEventsRaw = await eventsQuery
            .Select(e => new
            {
                e.Id,
                e.Title,
                DepartmentName = e.Department.Name,
                Confirmed = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
                e.TotalCapacity
            })
            .OrderByDescending(x => x.Confirmed)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topEvents = topEventsRaw.Select(x => new TopEventMetricDto
        {
            EventId = x.Id,
            Title = x.Title,
            DepartmentName = x.DepartmentName,
            ConfirmedCount = x.Confirmed,
            TotalCapacity = x.TotalCapacity,
            CapacityUtilization = x.TotalCapacity > 0 ? Math.Round(x.Confirmed * 100.0 / x.TotalCapacity, 1) : 0.0
        }).ToList();

        // Department performance overview
        var deptPerformance = await GetDepartmentPerformanceReportAsync(filter, cancellationToken);

        return new DashboardAnalyticsSummaryDto
        {
            Events = new EventKpiDto
            {
                TotalEvents = totalEvents,
                PublishedEvents = publishedEvents,
                CompletedEvents = completedEvents,
                UpcomingEvents = upcomingEvents,
                CancelledEvents = cancelledEvents,
                DraftEvents = draftEvents,
                PendingApprovalEvents = pendingApprovalEvents
            },
            Registrations = new RegistrationKpiDto
            {
                TotalRegistrations = totalRegs,
                ConfirmedRegistrations = confirmedRegs,
                PendingRegistrations = pendingRegs,
                WaitlistedRegistrations = waitlistedRegs,
                CancelledRegistrations = cancelledRegs,
                ConversionRate = conversionRate
            },
            Attendance = new AttendanceKpiDto
            {
                EligibleRegistrations = eligibleAttendance,
                CheckedInCount = checkedInCount,
                NotAttendedCount = notAttendedCount,
                AttendancePercentage = attendancePercentage
            },
            Financials = new FinancialKpiDto
            {
                GrossCollectedRevenue = grossRevenue,
                RefundedAmount = refundedAmount,
                NetCollectedRevenue = netRevenue,
                SuccessfulTransactionsCount = successTxCount,
                PendingTransactionsCount = pendingTxCount,
                FailedTransactionsCount = failedTxCount,
                RefundedTransactionsCount = refundedTxCount,
                TotalTransactionsCount = totalTxCount
            },
            Feedback = new FeedbackKpiDto
            {
                TotalSubmissions = totalFeedback,
                AverageOverallRating = avgOverall,
                AverageContentRating = avgContent,
                AverageSpeakerRating = avgSpeaker,
                AverageVenueRating = avgVenue,
                RatingDistribution = ratingDict,
                ParticipationRate = feedbackParticipation
            },
            Certificates = new CertificateKpiDto
            {
                TotalIssued = totalCerts,
                ActiveCount = activeCerts,
                RevokedCount = revokedCerts
            },
            MonthlyRegistrationTrend = monthlyRegs,
            MonthlyRevenueTrend = monthlyRevenue,
            EventStatusDistribution = eventStatusDist,
            DepartmentRegistrationDistribution = deptRegsDist,
            AttendanceDistribution = attendanceDist,
            PaymentStatusDistribution = payStatusDist,
            FeedbackRatingDistribution = feedbackDist,
            CertificateIssuanceTrend = certTrend,
            TopEventsByRegistrations = topEvents,
            DepartmentPerformance = deptPerformance.ToList()
        };
    }

    /// <inheritdoc />
    public async Task<CoordinatorAnalyticsDto> GetCoordinatorAnalyticsAsync(
        string coordinatorUserId,
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            throw new ArgumentException("Coordinator user identifier is required.", nameof(coordinatorUserId));
        }

        var assignedEventIds = await _context.EventCoordinators
            .AsNoTracking()
            .Where(ec => ec.UserId == coordinatorUserId)
            .Select(ec => ec.EventId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Security Guard: Check if coordinator attempted to select an unassigned event in filter
        if (filter.EventId.HasValue && !assignedEventIds.Contains(filter.EventId.Value))
        {
            _logger.LogWarning("Security Guard: Coordinator {UserId} attempted to access analytics for unassigned Event #{EventId}",
                coordinatorUserId, filter.EventId.Value);
            throw new UnauthorizedAccessException($"You are not authorized to access analytics for Event #{filter.EventId.Value}.");
        }

        var (start, end) = ResolveDateWindow(filter);

        var eventsQuery = BuildFilteredEventsQuery(filter, start, end, assignedEventIds);
        var regQuery = BuildFilteredRegistrationsQuery(filter, start, end, assignedEventIds);
        var feedbackQuery = BuildFilteredFeedbackQuery(filter, start, end, assignedEventIds);
        var certQuery = BuildFilteredCertificatesQuery(filter, start, end, assignedEventIds);

        var now = DateTime.UtcNow;

        var totalAssigned = await eventsQuery.CountAsync(cancellationToken);
        var publishedCount = await eventsQuery.CountAsync(e => e.Status == EventStatus.Published, cancellationToken);
        var completedCount = await eventsQuery.CountAsync(e => e.Status == EventStatus.Completed || e.EventEndDate < now, cancellationToken);
        var upcomingCount = await eventsQuery.CountAsync(e => e.Status != EventStatus.Cancelled && e.EventStartDate > now, cancellationToken);

        var totalRegs = await regQuery.CountAsync(cancellationToken);
        var confirmedRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Confirmed, cancellationToken);
        var waitlistedRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Waitlisted, cancellationToken);
        var cancelledRegs = await regQuery.CountAsync(r => r.Status == RegistrationStatus.Cancelled, cancellationToken);

        var eligibleAttendance = confirmedRegs;
        var checkedInCount = await regQuery
            .Where(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn)
            .CountAsync(cancellationToken);
        var attPercentage = eligibleAttendance > 0 ? Math.Round(checkedInCount * 100.0 / eligibleAttendance, 2) : 0.0;

        // Scoped event revenue
        var verifiedRevenue = await _context.Payments
            .AsNoTracking()
            .Where(p => assignedEventIds.Contains(p.Registration.EventId) && p.Status == PaymentStatus.Success)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // Feedback
        var totalFeedback = await feedbackQuery.CountAsync(cancellationToken);
        var avgFeedback = totalFeedback > 0 ? Math.Round(await feedbackQuery.AverageAsync(f => (double)f.OverallRating, cancellationToken), 2) : 0.0;

        // Certificates
        var certsIssued = await certQuery.CountAsync(cancellationToken);

        // Assigned Event Options for filter dropdown
        var assignedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => assignedEventIds.Contains(e.Id) && !e.IsDeleted)
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new EventOptionDto
            {
                Id = e.Id,
                Title = e.Title,
                Status = e.Status,
                StartDate = e.EventStartDate
            })
            .ToListAsync(cancellationToken);

        // Detailed event performance items for assigned events
        var eventPerfPaged = await GetEventPerformanceReportAsync(filter, 1, 100, coordinatorUserId, cancellationToken);

        // Registration Trend (by month/day)
        var regTrendRaw = await regQuery
            .GroupBy(r => new { r.RegisteredAt.Year, r.RegisteredAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var regTrend = regTrendRaw.Select(x => new TimeSeriesPointDto
        {
            Period = $"{x.Year:D4}-{x.Month:D2}",
            Label = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"),
            Count = x.Count,
            Value = x.Count
        }).ToList();

        var attendanceDist = new List<DistributionItemDto>
        {
            new() { Category = "Checked In", Count = checkedInCount, Percentage = attPercentage },
            new() { Category = "Not Attended", Count = Math.Max(0, eligibleAttendance - checkedInCount), Percentage = eligibleAttendance > 0 ? Math.Round(Math.Max(0, eligibleAttendance - checkedInCount) * 100.0 / eligibleAttendance, 1) : 0.0 }
        };

        var ratingDistRaw = await feedbackQuery
            .GroupBy(f => f.OverallRating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ratingDict = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        foreach (var r in ratingDistRaw)
        {
            if (r.Rating >= 1 && r.Rating <= 5)
            {
                ratingDict[r.Rating] = r.Count;
            }
        }

        var feedbackDist = ratingDict.OrderBy(kv => kv.Key).Select(kv => new DistributionItemDto
        {
            Category = $"{kv.Key} Star{(kv.Key > 1 ? "s" : "")}",
            Count = kv.Value,
            Percentage = totalFeedback > 0 ? Math.Round(kv.Value * 100.0 / totalFeedback, 1) : 0.0
        }).ToList();

        return new CoordinatorAnalyticsDto
        {
            AssignedEventsCount = totalAssigned,
            PublishedEventsCount = publishedCount,
            CompletedEventsCount = completedCount,
            UpcomingEventsCount = upcomingCount,
            TotalRegistrations = totalRegs,
            ConfirmedRegistrations = confirmedRegs,
            WaitlistedRegistrations = waitlistedRegs,
            CancelledRegistrations = cancelledRegs,
            CheckedInCount = checkedInCount,
            EligibleAttendanceCount = eligibleAttendance,
            AttendancePercentage = attPercentage,
            VerifiedRevenue = verifiedRevenue,
            FeedbackSubmissions = totalFeedback,
            AverageFeedbackRating = avgFeedback,
            CertificatesIssued = certsIssued,
            AssignedEvents = assignedEvents,
            EventPerformance = eventPerfPaged.Items.ToList(),
            RegistrationTrend = regTrend,
            AttendanceDistribution = attendanceDist,
            FeedbackRatingDistribution = feedbackDist
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<EventPerformanceReportItemDto>> GetEventPerformanceReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        List<int>? allowedEventIds = null;
        if (!string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            allowedEventIds = await _context.EventCoordinators
                .AsNoTracking()
                .Where(ec => ec.UserId == coordinatorUserId)
                .Select(ec => ec.EventId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (filter.EventId.HasValue && !allowedEventIds.Contains(filter.EventId.Value))
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized to access Event #{filter.EventId.Value}.");
            }
        }

        var (start, end) = ResolveDateWindow(filter);
        var query = BuildFilteredEventsQuery(filter, start, end, allowedEventIds);

        var totalCount = await query.CountAsync(cancellationToken);

        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(e => e.EventStartDate)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(e => new EventPerformanceReportItemDto
            {
                EventId = e.Id,
                EventTitle = e.Title,
                CategoryName = e.EventCategory.Name,
                DepartmentName = e.Department.Name,
                StartDate = e.EventStartDate,
                EndDate = e.EventEndDate,
                Status = e.Status,
                TotalCapacity = e.TotalCapacity,
                TotalRegistrations = e.Registrations.Count,
                ConfirmedRegistrations = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
                WaitlistedRegistrations = e.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
                CancelledRegistrations = e.Registrations.Count(r => r.Status == RegistrationStatus.Cancelled),
                AttendanceCount = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn),
                AttendancePercentage = e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed) > 0
                    ? Math.Round(e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn) * 100.0 / e.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed), 2)
                    : 0.0,
                VerifiedRevenue = e.Registrations
                    .SelectMany(r => r.Payments)
                    .Where(pay => pay.Status == PaymentStatus.Success)
                    .Sum(pay => (decimal?)pay.Amount) ?? 0m,
                FeedbackCount = e.Registrations.Count(r => r.Feedback != null),
                AverageRating = e.Registrations.Where(r => r.Feedback != null).Select(r => (double?)r.Feedback!.OverallRating).Average() ?? 0.0,
                CertificatesIssued = e.Registrations.Count(r => r.Certificate != null && r.Certificate.Status == CertificateStatus.Active)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EventPerformanceReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentPerformanceReportItemDto>> GetDepartmentPerformanceReportAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveDateWindow(filter);

        var query = _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive);

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(d => d.Id == filter.DepartmentId.Value);
        }

        var results = await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentPerformanceReportItemDto
            {
                DepartmentId = d.Id,
                DepartmentName = d.Name,
                DepartmentCode = d.Code,
                TotalEvents = d.Events.Count(e => !e.IsDeleted &&
                    (!start.HasValue || e.EventStartDate >= start.Value) &&
                    (!end.HasValue || e.EventStartDate <= end.Value)),
                TotalRegistrations = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Count(),
                ConfirmedRegistrations = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Count(r => r.Status == RegistrationStatus.Confirmed),
                AttendanceCount = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Count(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn),
                AttendancePercentage = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Count(r => r.Status == RegistrationStatus.Confirmed) > 0
                    ? Math.Round(d.Events
                        .Where(e => !e.IsDeleted &&
                            (!start.HasValue || e.EventStartDate >= start.Value) &&
                            (!end.HasValue || e.EventStartDate <= end.Value))
                        .SelectMany(e => e.Registrations)
                        .Count(r => r.Status == RegistrationStatus.Confirmed && r.Attendance != null && r.Attendance.Status == AttendanceStatus.CheckedIn) * 100.0 /
                      d.Events
                        .Where(e => !e.IsDeleted &&
                            (!start.HasValue || e.EventStartDate >= start.Value) &&
                            (!end.HasValue || e.EventStartDate <= end.Value))
                        .SelectMany(e => e.Registrations)
                        .Count(r => r.Status == RegistrationStatus.Confirmed), 2)
                    : 0.0,
                VerifiedRevenue = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .SelectMany(r => r.Payments)
                    .Where(p => p.Status == PaymentStatus.Success)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                AverageFeedbackRating = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Where(r => r.Feedback != null)
                    .Select(r => (double?)r.Feedback!.OverallRating)
                    .Average() ?? 0.0,
                CertificatesIssued = d.Events
                    .Where(e => !e.IsDeleted &&
                        (!start.HasValue || e.EventStartDate >= start.Value) &&
                        (!end.HasValue || e.EventStartDate <= end.Value))
                    .SelectMany(e => e.Registrations)
                    .Count(r => r.Certificate != null && r.Certificate.Status == CertificateStatus.Active)
            })
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <inheritdoc />
    public async Task<PagedResult<RegistrationReportItemDto>> GetRegistrationReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        List<int>? allowedEventIds = null;
        if (!string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            allowedEventIds = await _context.EventCoordinators
                .AsNoTracking()
                .Where(ec => ec.UserId == coordinatorUserId)
                .Select(ec => ec.EventId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (filter.EventId.HasValue && !allowedEventIds.Contains(filter.EventId.Value))
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized to access registrations for Event #{filter.EventId.Value}.");
            }
        }

        var (start, end) = ResolveDateWindow(filter);
        var query = BuildFilteredRegistrationsQuery(filter, start, end, allowedEventIds);

        var totalCount = await query.CountAsync(cancellationToken);
        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(r => new RegistrationReportItemDto
            {
                RegistrationNumber = r.RegistrationNumber,
                EventTitle = r.Event.Title,
                StudentName = r.User.FullName,
                StudentRollNumber = r.User.RollNumber ?? string.Empty,
                DepartmentName = r.Event.Department.Name,
                RegisteredAt = r.RegisteredAt,
                Status = r.Status,
                PaymentStatus = r.Payments.Any(pay => pay.Status == PaymentStatus.Success) ? PaymentStatus.Success :
                                (r.Payments.Any(pay => pay.Status == PaymentStatus.Pending) ? PaymentStatus.Pending :
                                (r.Payments.Any(pay => pay.Status == PaymentStatus.Refunded) ? PaymentStatus.Refunded : PaymentStatus.NotApplicable)),
                Amount = r.Payments.Where(pay => pay.Status == PaymentStatus.Success).Select(pay => (decimal?)pay.Amount).FirstOrDefault() ?? 0m
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RegistrationReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<AttendanceReportItemDto>> GetAttendanceReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        List<int>? allowedEventIds = null;
        if (!string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            allowedEventIds = await _context.EventCoordinators
                .AsNoTracking()
                .Where(ec => ec.UserId == coordinatorUserId)
                .Select(ec => ec.EventId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (filter.EventId.HasValue && !allowedEventIds.Contains(filter.EventId.Value))
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized to access attendance for Event #{filter.EventId.Value}.");
            }
        }

        var (start, end) = ResolveDateWindow(filter);
        var query = _context.Attendances
            .AsNoTracking()
            .Where(a => !a.Registration.Event.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(a => allowedEventIds.Contains(a.Registration.EventId));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(a => a.Registration.EventId == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(a => a.Registration.Event.DepartmentId == filter.DepartmentId.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(a => a.CheckedInAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(a => a.CheckedInAt <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a => a.Registration.RegistrationNumber.ToLower().Contains(search) ||
                                     a.Registration.User.FullName.ToLower().Contains(search) ||
                                     (a.Registration.User.RollNumber != null && a.Registration.User.RollNumber.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CheckedInAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(a => new AttendanceReportItemDto
            {
                RegistrationNumber = a.Registration.RegistrationNumber,
                EventTitle = a.Registration.Event.Title,
                StudentName = a.Registration.User.FullName,
                StudentRollNumber = a.Registration.User.RollNumber ?? string.Empty,
                CheckedInAt = a.CheckedInAt,
                CheckInMethod = a.CheckInMethod,
                CheckedInByName = a.CheckedInByUser != null ? a.CheckedInByUser.FullName : null,
                Remarks = a.Remarks,
                Status = a.Status
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AttendanceReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<FinancialReportItemDto>> GetFinancialReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolveDateWindow(filter);
        var query = BuildFilteredPaymentsQuery(filter, start, end, null);

        var totalCount = await query.CountAsync(cancellationToken);
        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(pay => pay.InitiatedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(pay => new FinancialReportItemDto
            {
                TransactionId = pay.TransactionId,
                OrderId = pay.OrderId,
                RegistrationNumber = pay.Registration.RegistrationNumber,
                EventTitle = pay.Registration.Event.Title,
                StudentName = pay.Registration.User.FullName,
                Amount = pay.Amount,
                Currency = pay.Currency,
                Status = pay.Status,
                PaymentMethod = pay.PaymentMethod,
                InitiatedAt = pay.InitiatedAt,
                CompletedAt = pay.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FinancialReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<FeedbackSummaryReportItemDto>> GetFeedbackReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        List<int>? allowedEventIds = null;
        if (!string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            allowedEventIds = await _context.EventCoordinators
                .AsNoTracking()
                .Where(ec => ec.UserId == coordinatorUserId)
                .Select(ec => ec.EventId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (filter.EventId.HasValue && !allowedEventIds.Contains(filter.EventId.Value))
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized to access feedback for Event #{filter.EventId.Value}.");
            }
        }

        var (start, end) = ResolveDateWindow(filter);
        var query = BuildFilteredFeedbackQuery(filter, start, end, allowedEventIds);

        var totalCount = await query.CountAsync(cancellationToken);
        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var rawItems = await query
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(f => new
            {
                f.Id,
                EventTitle = f.Event.Title,
                StudentFullName = f.User.FullName,
                StudentRoll = f.User.RollNumber,
                f.IsAnonymous,
                f.OverallRating,
                f.ContentQualityRating,
                f.SpeakerRating,
                f.VenueRating,
                f.Comments,
                f.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        // Anonymous Privacy Defense: Mask student identification when anonymous
        var items = rawItems.Select(f => new FeedbackSummaryReportItemDto
        {
            FeedbackId = f.Id,
            EventTitle = f.EventTitle,
            StudentName = f.IsAnonymous ? "Anonymous Student" : f.StudentFullName,
            RollNumber = f.IsAnonymous ? "Protected" : (f.StudentRoll ?? string.Empty),
            IsAnonymous = f.IsAnonymous,
            OverallRating = f.OverallRating,
            ContentQualityRating = f.ContentQualityRating,
            SpeakerRating = f.SpeakerRating,
            VenueRating = f.VenueRating,
            Comments = f.Comments,
            SubmittedAt = f.SubmittedAt
        }).ToList();

        return new PagedResult<FeedbackSummaryReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<CertificateReportItemDto>> GetCertificateReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        List<int>? allowedEventIds = null;
        if (!string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            allowedEventIds = await _context.EventCoordinators
                .AsNoTracking()
                .Where(ec => ec.UserId == coordinatorUserId)
                .Select(ec => ec.EventId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (filter.EventId.HasValue && !allowedEventIds.Contains(filter.EventId.Value))
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized to access certificates for Event #{filter.EventId.Value}.");
            }
        }

        var (start, end) = ResolveDateWindow(filter);
        var query = BuildFilteredCertificatesQuery(filter, start, end, allowedEventIds);

        var totalCount = await query.CountAsync(cancellationToken);
        var p = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(c => c.IssuedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(c => new CertificateReportItemDto
            {
                CertificateNumber = c.CertificateNumber,
                EventTitle = c.Event.Title,
                StudentName = c.User.FullName,
                RollNumber = c.User.RollNumber ?? string.Empty,
                Status = c.Status,
                IssuedAt = c.IssuedAt,
                IssuedByName = c.IssuedByUser != null ? c.IssuedByUser.FullName : null,
                RevokedAt = c.RevokedAt,
                RevocationReason = c.RevocationReason
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CertificateReportItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = p,
            PageSize = size
        };
    }

    #region Private Helper Methods

    private static (DateTime? start, DateTime? end) ResolveDateWindow(AnalyticsFilterDto filter)
    {
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(filter.PeriodPreset))
        {
            switch (filter.PeriodPreset.Trim().ToLowerInvariant())
            {
                case "last7days":
                    return (now.Date.AddDays(-7), now);
                case "last30days":
                    return (now.Date.AddDays(-30), now);
                case "last90days":
                    return (now.Date.AddDays(-90), now);
                case "currentmonth":
                    var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
                    return (startOfMonth, endOfMonth);
                case "currentyear":
                    var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var endOfYear = new DateTime(now.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                    return (startOfYear, endOfYear);
                case "all":
                    return (null, null);
            }
        }

        var start = filter.StartDate;
        var end = filter.EndDate;

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            // Swap to maintain valid window
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private IQueryable<Event> BuildFilteredEventsQuery(
        AnalyticsFilterDto filter,
        DateTime? start,
        DateTime? end,
        List<int>? allowedEventIds)
    {
        var query = _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(e => allowedEventIds.Contains(e.Id));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(e => e.Id == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventCategoryId.HasValue)
        {
            query = query.Where(e => e.EventCategoryId == filter.EventCategoryId.Value);
        }

        if (filter.EventStatus.HasValue)
        {
            query = query.Where(e => e.Status == filter.EventStatus.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(e => e.EventStartDate >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(e => e.EventStartDate <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(search) || (e.Venue != null && e.Venue.ToLower().Contains(search)));
        }

        return query;
    }

    private IQueryable<EventRegistration> BuildFilteredRegistrationsQuery(
        AnalyticsFilterDto filter,
        DateTime? start,
        DateTime? end,
        List<int>? allowedEventIds)
    {
        var query = _context.EventRegistrations
            .AsNoTracking()
            .Where(r => !r.Event.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(r => allowedEventIds.Contains(r.EventId));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(r => r.EventId == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(r => r.Event.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventCategoryId.HasValue)
        {
            query = query.Where(r => r.Event.EventCategoryId == filter.EventCategoryId.Value);
        }

        if (filter.EventStatus.HasValue)
        {
            query = query.Where(r => r.Event.Status == filter.EventStatus.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(r => r.RegisteredAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(r => r.RegisteredAt <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(r => r.RegistrationNumber.ToLower().Contains(search) ||
                                     r.User.FullName.ToLower().Contains(search) ||
                                     (r.User.RollNumber != null && r.User.RollNumber.ToLower().Contains(search)) ||
                                     r.Event.Title.ToLower().Contains(search));
        }

        return query;
    }

    private IQueryable<Payment> BuildFilteredPaymentsQuery(
        AnalyticsFilterDto filter,
        DateTime? start,
        DateTime? end,
        List<int>? allowedEventIds)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Where(p => !p.Registration.Event.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(p => allowedEventIds.Contains(p.Registration.EventId));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(p => p.Registration.EventId == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(p => p.Registration.Event.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventCategoryId.HasValue)
        {
            query = query.Where(p => p.Registration.Event.EventCategoryId == filter.EventCategoryId.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(p => p.InitiatedAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(p => p.InitiatedAt <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(p => (p.TransactionId != null && p.TransactionId.ToLower().Contains(search)) ||
                                     (p.OrderId != null && p.OrderId.ToLower().Contains(search)) ||
                                     p.Registration.RegistrationNumber.ToLower().Contains(search) ||
                                     p.Registration.User.FullName.ToLower().Contains(search));
        }

        return query;
    }

    private IQueryable<EventFeedback> BuildFilteredFeedbackQuery(
        AnalyticsFilterDto filter,
        DateTime? start,
        DateTime? end,
        List<int>? allowedEventIds)
    {
        var query = _context.EventFeedbacks
            .AsNoTracking()
            .Where(f => !f.Event.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(f => allowedEventIds.Contains(f.EventId));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(f => f.EventId == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(f => f.Event.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventCategoryId.HasValue)
        {
            query = query.Where(f => f.Event.EventCategoryId == filter.EventCategoryId.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(f => f.SubmittedAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(f => f.SubmittedAt <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(f => (f.Comments != null && f.Comments.ToLower().Contains(search)) ||
                                     f.Event.Title.ToLower().Contains(search));
        }

        return query;
    }

    private IQueryable<Certificate> BuildFilteredCertificatesQuery(
        AnalyticsFilterDto filter,
        DateTime? start,
        DateTime? end,
        List<int>? allowedEventIds)
    {
        var query = _context.Certificates
            .AsNoTracking()
            .Where(c => !c.Event.IsDeleted);

        if (allowedEventIds != null)
        {
            query = query.Where(c => allowedEventIds.Contains(c.EventId));
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(c => c.EventId == filter.EventId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(c => c.Event.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventCategoryId.HasValue)
        {
            query = query.Where(c => c.Event.EventCategoryId == filter.EventCategoryId.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(c => c.IssuedAt >= start.Value);
        }

        if (end.HasValue)
        {
            query = query.Where(c => c.IssuedAt <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim().ToLower();
            query = query.Where(c => c.CertificateNumber.ToLower().Contains(search) ||
                                     c.User.FullName.ToLower().Contains(search) ||
                                     (c.User.RollNumber != null && c.User.RollNumber.ToLower().Contains(search)) ||
                                     c.Event.Title.ToLower().Contains(search));
        }

        return query;
    }

    #endregion
}

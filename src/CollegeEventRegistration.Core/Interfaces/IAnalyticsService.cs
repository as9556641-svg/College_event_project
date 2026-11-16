using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for aggregated analytics, KPIs, time series trends, and scoped reporting.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Computes campus-wide analytics summary, KPI cards, and distribution trends for Admin/SuperAdmin.
    /// </summary>
    Task<DashboardAnalyticsSummaryDto> GetCampusAnalyticsSummaryAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes event-scoped analytics and KPIs for an authorized event Coordinator.
    /// Strictly verifies that the coordinator is assigned to the event(s).
    /// </summary>
    Task<CoordinatorAnalyticsDto> GetCoordinatorAnalyticsAsync(
        string coordinatorUserId,
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates paginated event-level performance metrics with attendance, financial, and feedback aggregates.
    /// Supports coordinator scoping when coordinatorUserId is provided.
    /// </summary>
    Task<PagedResult<EventPerformanceReportItemDto>> GetEventPerformanceReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates campus-wide department comparison performance metrics for Administrators.
    /// </summary>
    Task<IReadOnlyList<DepartmentPerformanceReportItemDto>> GetDepartmentPerformanceReportAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a paginated registration report.
    /// </summary>
    Task<PagedResult<RegistrationReportItemDto>> GetRegistrationReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a paginated attendance report.
    /// </summary>
    Task<PagedResult<AttendanceReportItemDto>> GetAttendanceReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a paginated financial ledger report (Admin only).
    /// </summary>
    Task<PagedResult<FinancialReportItemDto>> GetFinancialReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a paginated feedback evaluation report (masks anonymous respondents).
    /// </summary>
    Task<PagedResult<FeedbackSummaryReportItemDto>> GetFeedbackReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a paginated certificate issuance report.
    /// </summary>
    Task<PagedResult<CertificateReportItemDto>> GetCertificateReportAsync(
        AnalyticsFilterDto filter,
        int pageNumber,
        int pageSize,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default);
}

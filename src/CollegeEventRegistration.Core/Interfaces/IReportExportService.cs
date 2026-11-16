using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Service for exporting filtered analytics data into CSV and PDF formats with formula injection protection.
/// </summary>
public interface IReportExportService
{
    Task<byte[]> ExportEventPerformanceCsvAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportDepartmentPerformanceCsvAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportRegistrationCsvAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAttendanceCsvAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportFinancialCsvAsync(AnalyticsFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportFeedbackCsvAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportCertificateCsvAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExecutiveSummaryPdfAsync(AnalyticsFilterDto filter, string? coordinatorUserId = null, CancellationToken cancellationToken = default);
}

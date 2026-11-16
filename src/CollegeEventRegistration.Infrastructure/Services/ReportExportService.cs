using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Service for exporting analytics and reports into CSV and QuestPDF formats.
/// Includes strict CSV formula injection (DDE) defenses and anonymous feedback protection.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAuditLogService _auditLogService;

    public ReportExportService(IAnalyticsService analyticsService, IAuditLogService auditLogService)
    {
        _analyticsService = analyticsService;
        _auditLogService = auditLogService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportEventPerformanceCsvAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetEventPerformanceReportAsync(filter, 1, 10000, coordinatorUserId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Event ID,Event Title,Category,Department,Start Date,End Date,Status,Capacity,Total Regs,Confirmed,Waitlisted,Cancelled,Attendance,Attendance %,Verified Revenue (INR),Feedback Count,Avg Rating,Certificates");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.EventId.ToString()),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.CategoryName),
                EscapeCsvCell(item.DepartmentName),
                EscapeCsvCell(item.StartDate.ToString("yyyy-MM-dd HH:mm")),
                EscapeCsvCell(item.EndDate.ToString("yyyy-MM-dd HH:mm")),
                EscapeCsvCell(item.Status.ToString()),
                EscapeCsvCell(item.TotalCapacity.ToString()),
                EscapeCsvCell(item.TotalRegistrations.ToString()),
                EscapeCsvCell(item.ConfirmedRegistrations.ToString()),
                EscapeCsvCell(item.WaitlistedRegistrations.ToString()),
                EscapeCsvCell(item.CancelledRegistrations.ToString()),
                EscapeCsvCell(item.AttendanceCount.ToString()),
                EscapeCsvCell(item.AttendancePercentage.ToString("F2") + "%"),
                EscapeCsvCell(item.VerifiedRevenue.ToString("F2")),
                EscapeCsvCell(item.FeedbackCount.ToString()),
                EscapeCsvCell(item.AverageRating.ToString("F2")),
                EscapeCsvCell(item.CertificatesIssued.ToString())
            ));
        }

        await _auditLogService.LogAsync("ExportEventPerformanceCsv", "Analytics", null, coordinatorUserId);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportDepartmentPerformanceCsvAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var items = await _analyticsService.GetDepartmentPerformanceReportAsync(filter, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Department ID,Department Name,Department Code,Total Events,Total Regs,Confirmed Regs,Attendance,Attendance %,Verified Revenue (INR),Avg Feedback Rating,Certificates Issued");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.DepartmentId.ToString()),
                EscapeCsvCell(item.DepartmentName),
                EscapeCsvCell(item.DepartmentCode),
                EscapeCsvCell(item.TotalEvents.ToString()),
                EscapeCsvCell(item.TotalRegistrations.ToString()),
                EscapeCsvCell(item.ConfirmedRegistrations.ToString()),
                EscapeCsvCell(item.AttendanceCount.ToString()),
                EscapeCsvCell(item.AttendancePercentage.ToString("F2") + "%"),
                EscapeCsvCell(item.VerifiedRevenue.ToString("F2")),
                EscapeCsvCell(item.AverageFeedbackRating.ToString("F2")),
                EscapeCsvCell(item.CertificatesIssued.ToString())
            ));
        }

        await _auditLogService.LogAsync("ExportDepartmentPerformanceCsv", "Analytics", null, null);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportRegistrationCsvAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetRegistrationReportAsync(filter, 1, 10000, coordinatorUserId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Registration Number,Event Title,Student Name,Roll Number,Department,Registered At (UTC),Status,Payment Status,Amount (INR)");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.RegistrationNumber),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.StudentName),
                EscapeCsvCell(item.StudentRollNumber),
                EscapeCsvCell(item.DepartmentName),
                EscapeCsvCell(item.RegisteredAt.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsvCell(item.Status.ToString()),
                EscapeCsvCell(item.PaymentStatus.ToString()),
                EscapeCsvCell(item.Amount.ToString("F2"))
            ));
        }

        await _auditLogService.LogAsync("ExportRegistrationCsv", "Analytics", null, coordinatorUserId);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportAttendanceCsvAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetAttendanceReportAsync(filter, 1, 10000, coordinatorUserId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Registration Number,Event Title,Student Name,Roll Number,Checked In At (UTC),Method,Checked In By,Status,Remarks");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.RegistrationNumber),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.StudentName),
                EscapeCsvCell(item.StudentRollNumber),
                EscapeCsvCell(item.CheckedInAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"),
                EscapeCsvCell(item.CheckInMethod?.ToString() ?? "N/A"),
                EscapeCsvCell(item.CheckedInByName ?? "N/A"),
                EscapeCsvCell(item.Status.ToString()),
                EscapeCsvCell(item.Remarks ?? string.Empty)
            ));
        }

        await _auditLogService.LogAsync("ExportAttendanceCsv", "Analytics", null, coordinatorUserId);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportFinancialCsvAsync(
        AnalyticsFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetFinancialReportAsync(filter, 1, 10000, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Transaction ID,Order ID,Registration Number,Event Title,Student Name,Amount,Currency,Status,Payment Method,Initiated At (UTC),Completed At (UTC)");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.TransactionId ?? "N/A"),
                EscapeCsvCell(item.OrderId ?? "N/A"),
                EscapeCsvCell(item.RegistrationNumber),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.StudentName),
                EscapeCsvCell(item.Amount.ToString("F2")),
                EscapeCsvCell(item.Currency),
                EscapeCsvCell(item.Status.ToString()),
                EscapeCsvCell(item.PaymentMethod ?? "N/A"),
                EscapeCsvCell(item.InitiatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsvCell(item.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A")
            ));
        }

        await _auditLogService.LogAsync("ExportFinancialCsv", "Analytics", null, null);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportFeedbackCsvAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetFeedbackReportAsync(filter, 1, 10000, coordinatorUserId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Feedback ID,Event Title,Student Name,Roll Number,Anonymous,Overall Rating,Content Quality,Speaker Rating,Venue Rating,Comments,Submitted At (UTC)");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.FeedbackId.ToString()),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.StudentName), // Pre-masked by service if anonymous
                EscapeCsvCell(item.RollNumber),  // Pre-masked by service if anonymous
                EscapeCsvCell(item.IsAnonymous ? "Yes" : "No"),
                EscapeCsvCell(item.OverallRating.ToString()),
                EscapeCsvCell(item.ContentQualityRating.ToString()),
                EscapeCsvCell(item.SpeakerRating.ToString()),
                EscapeCsvCell(item.VenueRating.ToString()),
                EscapeCsvCell(item.Comments ?? string.Empty),
                EscapeCsvCell(item.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            ));
        }

        await _auditLogService.LogAsync("ExportFeedbackCsv", "Analytics", null, coordinatorUserId);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportCertificateCsvAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _analyticsService.GetCertificateReportAsync(filter, 1, 10000, coordinatorUserId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Certificate Number,Event Title,Student Name,Roll Number,Status,Issued At (UTC),Issued By,Revoked At (UTC),Revocation Reason");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsvCell(item.CertificateNumber),
                EscapeCsvCell(item.EventTitle),
                EscapeCsvCell(item.StudentName),
                EscapeCsvCell(item.RollNumber),
                EscapeCsvCell(item.Status.ToString()),
                EscapeCsvCell(item.IssuedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsvCell(item.IssuedByName ?? "N/A"),
                EscapeCsvCell(item.RevokedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"),
                EscapeCsvCell(item.RevocationReason ?? string.Empty)
            ));
        }

        await _auditLogService.LogAsync("ExportCertificateCsv", "Analytics", null, coordinatorUserId);
        return EncodeWithBom(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportExecutiveSummaryPdfAsync(
        AnalyticsFilterDto filter,
        string? coordinatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        DashboardAnalyticsSummaryDto? summary = null;
        CoordinatorAnalyticsDto? coordSummary = null;

        if (string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            summary = await _analyticsService.GetCampusAnalyticsSummaryAsync(filter, cancellationToken);
        }
        else
        {
            coordSummary = await _analyticsService.GetCoordinatorAnalyticsAsync(coordinatorUserId, filter, cancellationToken);
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VAP UNIVERSITY").FontSize(20).Bold().FontColor("#0f2b48");
                            c.Item().Text("CAMPUS EVENT HUB — EXECUTIVE ANALYTICS REPORT").FontSize(11).FontColor("#555555");
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor("#777777");
                            c.Item().Text(coordinatorUserId != null ? "Scope: Assigned Events" : "Scope: Campus-Wide").FontSize(8).Bold().FontColor("#0f2b48");
                        });
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#0f2b48");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    // Executive Summary Highlights Table
                    col.Item().Text("Key Performance Indicators").FontSize(14).Bold().FontColor("#0f2b48");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0f2b48").Padding(5).Text("Metric Category").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background("#0f2b48").Padding(5).Text("Value").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background("#0f2b48").Padding(5).Text("Metric Category").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background("#0f2b48").Padding(5).Text("Value").FontColor(Colors.White).Bold().FontSize(9);
                        });

                        if (summary != null)
                        {
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Total Events").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Events.TotalEvents.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Published Events").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Events.PublishedEvents.ToString()).Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Total Registrations").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Registrations.TotalRegistrations.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Confirmed Registrations").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Registrations.ConfirmedRegistrations.ToString()).Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Conversion Rate").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"{summary.Registrations.ConversionRate:F1}%").Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Attendance Rate").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"{summary.Attendance.AttendancePercentage:F1}%").Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Checked In Attendees").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Attendance.CheckedInCount.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Gross Verified Revenue").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"INR {summary.Financials.GrossCollectedRevenue:N2}").Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Net Collected Revenue").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"INR {summary.Financials.NetCollectedRevenue:N2}").Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Average Event Rating").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"{summary.Feedback.AverageOverallRating:F2} / 5.0").Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Total Feedback Submissions").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Feedback.TotalSubmissions.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Active Certificates Issued").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(summary.Certificates.ActiveCount.ToString()).Bold().FontSize(9);
                        }
                        else if (coordSummary != null)
                        {
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Assigned Events").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(coordSummary.AssignedEventsCount.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Published Events").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(coordSummary.PublishedEventsCount.ToString()).Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Total Registrations").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(coordSummary.TotalRegistrations.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Confirmed Registrations").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(coordSummary.ConfirmedRegistrations.ToString()).Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Checked In Attendees").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text(coordSummary.CheckedInCount.ToString()).Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Attendance Rate").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"{coordSummary.AttendancePercentage:F1}%").Bold().FontSize(9);

                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Verified Revenue").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"INR {coordSummary.VerifiedRevenue:N2}").Bold().FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text("Average Event Rating").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor("#dddddd").Padding(4).Text($"{coordSummary.AverageFeedbackRating:F2} / 5.0").Bold().FontSize(9);
                        }
                    });

                    // Top Events Summary Table
                    col.Item().PaddingTop(10).Text("Top Performing Events").FontSize(14).Bold().FontColor("#0f2b48");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#0f2b48").Padding(4).Text("#").FontColor(Colors.White).Bold().FontSize(8);
                            header.Cell().Background("#0f2b48").Padding(4).Text("Event Title").FontColor(Colors.White).Bold().FontSize(8);
                            header.Cell().Background("#0f2b48").Padding(4).Text("Department").FontColor(Colors.White).Bold().FontSize(8);
                            header.Cell().Background("#0f2b48").Padding(4).Text("Confirmed").FontColor(Colors.White).Bold().FontSize(8);
                            header.Cell().Background("#0f2b48").Padding(4).Text("Capacity").FontColor(Colors.White).Bold().FontSize(8);
                            header.Cell().Background("#0f2b48").Padding(4).Text("Utilization").FontColor(Colors.White).Bold().FontSize(8);
                        });

                        var topList = summary?.TopEventsByRegistrations ?? coordSummary?.EventPerformance.Take(5).Select(e => new TopEventMetricDto
                        {
                            EventId = e.EventId,
                            Title = e.EventTitle,
                            DepartmentName = e.DepartmentName,
                            ConfirmedCount = e.ConfirmedRegistrations,
                            TotalCapacity = e.TotalCapacity,
                            CapacityUtilization = e.TotalCapacity > 0 ? Math.Round(e.ConfirmedRegistrations * 100.0 / e.TotalCapacity, 1) : 0.0
                        }).ToList() ?? new List<TopEventMetricDto>();

                        int idx = 1;
                        foreach (var ev in topList)
                        {
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(idx++.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(ev.Title).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(ev.DepartmentName).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(ev.ConfirmedCount.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text(ev.TotalCapacity.ToString()).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor("#eeeeee").Padding(3).Text($"{ev.CapacityUtilization:F1}%").FontSize(8);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("VAP UNIVERSITY — Campus Event Hub | Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        await _auditLogService.LogAsync("ExportExecutiveSummaryPdf", "Analytics", null, coordinatorUserId);
        return doc.GeneratePdf();
    }

    #region Formula Injection Protection & CSV Formatting

    /// <summary>
    /// Escapes CSV values and defends against CSV Formula Injection (DDE).
    /// If cell value starts with '=', '+', '-', '@', '\t', '\r', or '%', prepends a single quote.
    /// Wraps cell in double quotes if it contains commas, newlines, or quotes.
    /// </summary>
    public static string EscapeCsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cell = value;

        // CSV Formula Injection (DDE) Defense:
        // Characters that trigger spreadsheet formula execution: =, +, -, @, \t, \r, %
        if (cell.StartsWith('=') ||
            cell.StartsWith('+') ||
            cell.StartsWith('-') ||
            cell.StartsWith('@') ||
            cell.StartsWith('\t') ||
            cell.StartsWith('\r') ||
            cell.StartsWith('%'))
        {
            cell = "'" + cell;
        }

        // RFC 4180 Escaping:
        if (cell.Contains('"') || cell.Contains(',') || cell.Contains('\n') || cell.Contains('\r'))
        {
            cell = "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        return cell;
    }

    private static byte[] EncodeWithBom(string text)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(text);
        var result = new byte[preamble.Length + bytes.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(bytes, 0, result, preamble.Length, bytes.Length);
        return result;
    }

    #endregion
}

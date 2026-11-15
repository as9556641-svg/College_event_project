using System;
using System.IO;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// QuestPDF-based high-fidelity A4 landscape certificate rendering engine.
/// Produces official VAP UNIVERSITY completion and participation certificates.
/// </summary>
public class PdfCertificateGenerator : IPdfCertificateGenerator
{
    private readonly IQrCodeGenerator _qrCodeGenerator;

    public PdfCertificateGenerator(IQrCodeGenerator qrCodeGenerator)
    {
        _qrCodeGenerator = qrCodeGenerator;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <inheritdoc />
    public byte[] GenerateCertificatePdf(CertificateDetailDto model, string verificationUrl)
    {
        byte[]? qrBytes = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(verificationUrl))
            {
                qrBytes = _qrCodeGenerator.GenerateQrPngBytes(verificationUrl, 4);
            }
        }
        catch
        {
            qrBytes = null;
        }

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.PageColor(Colors.White);

                page.Content().Border(3).BorderColor("#0f2b48").Padding(6).Border(1).BorderColor("#d4af37").Padding(24).Column(col =>
                {
                    col.Spacing(10);

                    // Top Institution Brand
                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span("VAP UNIVERSITY").FontSize(26).Bold().FontColor("#0f2b48");
                    });

                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span("CAMPUS EVENT HUB • OFFICIAL CERTIFICATE OF COMPLETION")
                            .FontSize(11).Bold().FontColor("#64748b");
                    });

                    col.Item().PaddingVertical(4).LineHorizontal(1).LineColor("#e2e8f0");

                    // Recipient Section
                    col.Item().AlignCenter().PaddingTop(6).Text(t =>
                    {
                        t.Span("THIS IS TO CERTIFY THAT").FontSize(11).Italic().FontColor("#64748b");
                    });

                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span(model.StudentFullName).FontSize(24).Bold().FontColor("#0f2b48");
                    });

                    var studentSubtitle = $"Roll No: {model.StudentRollNumber ?? "N/A"}   •   Department: {model.StudentDepartment ?? model.EventDepartmentName}";
                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span(studentSubtitle).FontSize(11).FontColor("#475569");
                    });

                    // Event Participation Section
                    col.Item().AlignCenter().PaddingTop(6).Text(t =>
                    {
                        t.Span("has successfully participated in and completed").FontSize(11).Italic().FontColor("#64748b");
                    });

                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span(model.EventTitle).FontSize(18).Bold().FontColor("#1e293b");
                    });

                    var eventSubtitle = $"Organized by the Department of {model.EventDepartmentName} | Concluded on {model.EventEndDate:dd MMMM yyyy}";
                    col.Item().AlignCenter().Text(t =>
                    {
                        t.Span(eventSubtitle).FontSize(10.5f).FontColor("#475569");
                    });

                    col.Item().PaddingTop(12).LineHorizontal(1).LineColor("#e2e8f0");

                    // Bottom Row: Verification Info, QR, Authorized Signatory
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        // Left: Certificate details
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Spacing(3);
                            c.Item().Text(t =>
                            {
                                t.Span("Certificate No: ").FontSize(9).FontColor("#64748b");
                                t.Span(model.CertificateNumber).FontSize(9).Bold().FontColor("#0f2b48");
                            });
                            c.Item().Text(t =>
                            {
                                t.Span("Issued Date: ").FontSize(9).FontColor("#64748b");
                                t.Span(model.IssuedAt.ToString("dd MMM yyyy")).FontSize(9).FontColor("#0f2b48");
                            });
                            c.Item().Text(t =>
                            {
                                t.Span("Status: ").FontSize(9).FontColor("#64748b");
                                t.Span(model.Status.ToString()).FontSize(9).Bold().FontColor(model.Status == CertificateStatus.Active ? "#16a34a" : "#dc2626");
                            });
                            c.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("Verify authenticity: ").FontSize(7.5f).FontColor("#94a3b8");
                                t.Span(verificationUrl).FontSize(7.5f).Underline().FontColor("#2563eb");
                            });
                        });

                        // Center: Verification QR Code
                        if (qrBytes != null && qrBytes.Length > 0)
                        {
                            row.RelativeItem(1).AlignCenter().Column(qc =>
                            {
                                qc.Item().Width(60).Height(60).Image(qrBytes);
                                qc.Item().AlignCenter().Text("Scan to Verify").FontSize(7).FontColor("#94a3b8");
                            });
                        }
                        else
                        {
                            row.RelativeItem(1).Column(_ => { });
                        }

                        // Right: Authorized Signatory
                        row.RelativeItem(3).AlignRight().Column(sc =>
                        {
                            sc.Spacing(2);
                            sc.Item().AlignRight().Text("___________________________").FontSize(10).FontColor("#94a3b8");
                            sc.Item().AlignRight().Text(model.SignatoryName).FontSize(11).Bold().FontColor("#0f2b48");
                            sc.Item().AlignRight().Text(model.SignatoryTitle).FontSize(9.5f).FontColor("#475569");
                            sc.Item().AlignRight().Text("VAP UNIVERSITY").FontSize(8.5f).FontColor("#64748b");
                        });
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing Phase 12: Automated Certificate Generation.
/// Handles student eligibility checks, dynamic PDF issuance, secure token verification,
/// staff revocation with audit logging, and coordinator scoping.
/// </summary>
public class CertificateService : ICertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly IPdfCertificateGenerator _pdfGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(
        ApplicationDbContext context,
        IPdfCertificateGenerator pdfGenerator,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CertificateService> logger)
    {
        _context = context;
        _pdfGenerator = pdfGenerator;
        _auditLogService = auditLogService;
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CertificateEligibilityDto> CheckEligibilityAsync(int registrationId, string? requestingUserId = null)
    {
        var reg = await _context.EventRegistrations
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Include(r => r.Payments)
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (reg == null)
        {
            return new CertificateEligibilityDto
            {
                RegistrationId = registrationId,
                IsEligible = false,
                EligibilityReason = "Registration record not found."
            };
        }

        var dto = new CertificateEligibilityDto
        {
            RegistrationId = reg.Id,
            RegistrationNumber = reg.RegistrationNumber,
            EventId = reg.EventId,
            EventTitle = reg.Event?.Title ?? string.Empty,
            EventEndDate = reg.Event?.EventEndDate ?? DateTime.MinValue,
            IsIssued = reg.Certificate != null,
            CertificateId = reg.Certificate?.Id,
            CertificateNumber = reg.Certificate?.CertificateNumber,
            VerificationToken = reg.Certificate?.VerificationToken,
            Status = reg.Certificate?.Status
        };

        // Tenant isolation: requesting user must match owner if provided
        if (!string.IsNullOrEmpty(requestingUserId) && reg.UserId != requestingUserId)
        {
            dto.IsEligible = false;
            dto.EligibilityReason = "Access denied: Registration belongs to another student.";
            return dto;
        }

        // If already issued
        if (reg.Certificate != null)
        {
            if (reg.Certificate.Status == CertificateStatus.Revoked)
            {
                dto.IsEligible = false;
                dto.EligibilityReason = "Certificate was revoked by administration.";
            }
            else
            {
                dto.IsEligible = true;
                dto.EligibilityReason = "Certificate is active and ready for download.";
            }
            return dto;
        }

        // Rule 1: Registration must be Confirmed
        if (reg.Status != RegistrationStatus.Confirmed)
        {
            dto.IsEligible = false;
            dto.EligibilityReason = $"Registration status is '{reg.Status}'. Only Confirmed registrations are eligible.";
            return dto;
        }

        // Rule 2: Event must have concluded
        if (reg.Event != null && reg.Event.EventEndDate > DateTime.UtcNow)
        {
            dto.IsEligible = false;
            dto.EligibilityReason = $"Event is still ongoing or scheduled. Certificates become available after {reg.Event.EventEndDate:dd MMM yyyy, hh:mm tt} UTC.";
            return dto;
        }

        // Rule 3: Attendance must be verified as CheckedIn
        if (reg.Attendance == null || reg.Attendance.Status != AttendanceStatus.CheckedIn)
        {
            dto.IsEligible = false;
            dto.EligibilityReason = "Attendance check-in is required to receive a certificate of completion.";
            return dto;
        }

        // Rule 4: Paid events require successful payment
        if (reg.Event != null && reg.Event.EventType == EventType.Paid && !reg.HasSuccessfulPayment)
        {
            dto.IsEligible = false;
            dto.EligibilityReason = "Confirmed payment is required for paid event certificates.";
            return dto;
        }

        dto.IsEligible = true;
        dto.EligibilityReason = "Eligible for certificate issuance.";
        return dto;
    }

    /// <inheritdoc />
    public async Task<List<CertificateEligibilityDto>> GetStudentCertificatesAsync(string studentUserId)
    {
        if (string.IsNullOrWhiteSpace(studentUserId)) return new List<CertificateEligibilityDto>();

        var registrations = await _context.EventRegistrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Include(r => r.Attendance)
            .Include(r => r.Payments)
            .Include(r => r.Certificate)
            .Where(r => r.UserId == studentUserId && r.Status == RegistrationStatus.Confirmed)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync();

        var results = new List<CertificateEligibilityDto>();
        foreach (var reg in registrations)
        {
            var isIssued = reg.Certificate != null;
            var isAttended = reg.Attendance != null && reg.Attendance.Status == AttendanceStatus.CheckedIn;
            var isEnded = reg.Event != null && reg.Event.EventEndDate <= DateTime.UtcNow;
            var isPaidValid = reg.Event?.EventType != EventType.Paid || reg.HasSuccessfulPayment;

            var isEligible = isAttended && isEnded && isPaidValid;
            string reason;

            if (isIssued)
            {
                reason = reg.Certificate!.Status == CertificateStatus.Revoked
                    ? "Certificate Revoked"
                    : "Issued";
            }
            else if (!isEnded)
            {
                reason = "Event in progress";
            }
            else if (!isAttended)
            {
                reason = "Attendance not recorded";
            }
            else if (!isPaidValid)
            {
                reason = "Payment pending";
            }
            else
            {
                reason = "Ready to claim";
            }

            results.Add(new CertificateEligibilityDto
            {
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                EventId = reg.EventId,
                EventTitle = reg.Event?.Title ?? string.Empty,
                EventEndDate = reg.Event?.EventEndDate ?? DateTime.MinValue,
                IsEligible = isEligible,
                EligibilityReason = reason,
                IsIssued = isIssued,
                CertificateId = reg.Certificate?.Id,
                CertificateNumber = reg.Certificate?.CertificateNumber,
                VerificationToken = reg.Certificate?.VerificationToken,
                Status = reg.Certificate?.Status
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<CertificateResultDto> IssueCertificateAsync(int registrationId, string? issuedByUserId = null)
    {
        var reg = await _context.EventRegistrations
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(r => r.Event)
                .ThenInclude(e => e.CertificateTemplate)
            .Include(r => r.User)
            .Include(r => r.Attendance)
            .Include(r => r.Payments)
            .Include(r => r.Certificate)
            .FirstOrDefaultAsync(r => r.Id == registrationId);

        if (reg == null)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.RegistrationNotFound, "Registration record not found.");
        }

        // If certificate already exists, return existing certificate details idempotently
        if (reg.Certificate != null)
        {
            return CertificateResultDto.Ok(
                reg.Certificate.Id,
                reg.Certificate.CertificateNumber,
                reg.Certificate.VerificationToken,
                reg.Certificate.PdfFilePath,
                "Certificate already issued.");
        }

        // Rule validations
        if (reg.Status != RegistrationStatus.Confirmed)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.RegistrationNotConfirmed, $"Registration is {reg.Status}. Only Confirmed registrations can be issued certificates.");
        }

        if (reg.Event != null && reg.Event.EventEndDate > DateTime.UtcNow)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.EventNotEnded, "Certificates cannot be issued before the event has concluded.");
        }

        if (reg.Attendance == null || reg.Attendance.Status != AttendanceStatus.CheckedIn)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.AttendanceRequired, "Attendance check-in is mandatory for certificate issuance.");
        }

        if (reg.Event != null && reg.Event.EventType == EventType.Paid && !reg.HasSuccessfulPayment)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.PaymentRequired, "Payment confirmation is required for paid event certificates.");
        }

        // Generate non-guessable certificate number and cryptographic verification token
        var year = DateTime.UtcNow.Year;
        var prefix = reg.Event?.CertificateTemplate?.CertificateCodePrefix?.Trim();
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "VAP-CERT";

        string certificateNumber;
        do
        {
            var randomCode = GenerateSecureCode(6);
            certificateNumber = $"{prefix}-{year}-{reg.EventId}-{randomCode}";
        } while (await _context.Certificates.AnyAsync(c => c.CertificateNumber == certificateNumber));

        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        // Signatory metadata
        var signatoryName = reg.Event?.CertificateTemplate?.SignatoryName;
        var signatoryTitle = reg.Event?.CertificateTemplate?.SignatoryTitle;

        if (string.IsNullOrWhiteSpace(signatoryName)) signatoryName = "Dr. K. Sharma";
        if (string.IsNullOrWhiteSpace(signatoryTitle)) signatoryTitle = "Dean of Academic Affairs";

        // Construct DTO for PDF generator
        var detailDto = new CertificateDetailDto
        {
            RegistrationId = reg.Id,
            RegistrationNumber = reg.RegistrationNumber,
            EventId = reg.EventId,
            EventTitle = reg.Event?.Title ?? "University Event",
            EventCategoryName = reg.Event?.EventCategory?.Name ?? "Academic",
            EventDepartmentName = reg.Event?.Department?.Name ?? "VAP University",
            EventStartDate = reg.Event?.EventStartDate ?? DateTime.UtcNow,
            EventEndDate = reg.Event?.EventEndDate ?? DateTime.UtcNow,
            UserId = reg.UserId,
            StudentFullName = reg.User?.FullName ?? "Student",
            StudentRollNumber = reg.User?.RollNumber,
            StudentDepartment = reg.User?.Department,
            StudentCollegeName = reg.User?.CollegeName,
            CertificateNumber = certificateNumber,
            VerificationToken = verificationToken,
            IssuedAt = DateTime.UtcNow,
            Status = CertificateStatus.Active,
            SignatoryName = signatoryName,
            SignatoryTitle = signatoryTitle
        };

        var verificationUrl = $"{GetBaseUrl()}/Certificates/Verify/{verificationToken}";

        // Render PDF
        byte[] pdfBytes;
        try
        {
            pdfBytes = _pdfGenerator.GenerateCertificatePdf(detailDto, verificationUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render PDF certificate for Registration {RegistrationId}", registrationId);
            return CertificateResultDto.Fail(CertificateStatusCode.Success, $"PDF generation failed: {ex.Message}");
        }

        // Save PDF to wwwroot/certificates/{year}/
        var webRoot = GetWebRootPath();
        var certsDir = Path.Combine(webRoot, "certificates", year.ToString());
        Directory.CreateDirectory(certsDir);

        var safeFileName = $"cert_{reg.EventId}_{reg.Id}_{verificationToken[..8]}.pdf";
        var physicalPath = Path.Combine(certsDir, safeFileName);

        // Anti-path traversal validation
        var canonicalWebRoot = Path.GetFullPath(webRoot);
        var canonicalFilePath = Path.GetFullPath(physicalPath);
        if (!canonicalFilePath.StartsWith(canonicalWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid certificate destination path outside web root.");
        }

        await File.WriteAllBytesAsync(physicalPath, pdfBytes);
        var relativePath = $"certificates/{year}/{safeFileName}";

        // Persist Certificate entity
        var certificate = new Certificate
        {
            RegistrationId = reg.Id,
            EventId = reg.EventId,
            UserId = reg.UserId,
            CertificateNumber = certificateNumber,
            VerificationToken = verificationToken,
            PdfFilePath = relativePath,
            IssuedAt = DateTime.UtcNow,
            IssuedByUserId = issuedByUserId,
            Status = CertificateStatus.Active
        };

        try
        {
            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrency race: Another thread or request committed the certificate first
            _context.ChangeTracker.Clear();
            var existing = await _context.Certificates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.RegistrationId == reg.Id);

            if (existing != null)
            {
                return CertificateResultDto.Ok(
                    existing.Id,
                    existing.CertificateNumber,
                    existing.VerificationToken,
                    existing.PdfFilePath,
                    "Certificate already issued.");
            }
            throw;
        }

        // Audit Log
        await _auditLogService.LogAsync(
            action: "IssueCertificate",
            entityName: nameof(Certificate),
            entityId: certificate.Id.ToString(),
            userId: issuedByUserId ?? reg.UserId,
            newValues: $"Issued certificate '{certificateNumber}' for registration #{reg.Id} (Student: {reg.User?.FullName}).");

        return CertificateResultDto.Ok(
            certificate.Id,
            certificateNumber,
            verificationToken,
            relativePath,
            "Certificate successfully generated and issued.");
    }

    /// <inheritdoc />
    public async Task<BatchCertificateIssueResultDto> BatchIssueCertificatesForEventAsync(int eventId, string issuedByUserId)
    {
        var ev = await _context.Events
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (ev == null)
        {
            return new BatchCertificateIssueResultDto
            {
                EventId = eventId,
                Messages = { "Event not found." }
            };
        }

        // Coordinator scoped check if not admin
        var user = await _context.Users.FindAsync(issuedByUserId);
        var isAdmin = user != null && await _context.UserRoles.AnyAsync(ur => ur.UserId == issuedByUserId &&
            _context.Roles.Any(r => r.Id == ur.RoleId && (r.Name == nameof(UserRole.Admin) || r.Name == nameof(UserRole.SuperAdmin))));

        if (!isAdmin && !ev.Coordinators.Any(c => c.UserId == issuedByUserId))
        {
            throw new UnauthorizedAccessException("Coordinators can only issue certificates for their assigned events.");
        }

        if (ev.EventEndDate > DateTime.UtcNow)
        {
            return new BatchCertificateIssueResultDto
            {
                EventId = eventId,
                Messages = { $"Event has not ended yet. Ends on {ev.EventEndDate:dd MMM yyyy, hh:mm tt} UTC." }
            };
        }

        var eligibleRegistrations = await _context.EventRegistrations
            .Include(r => r.Attendance)
            .Include(r => r.Payments)
            .Include(r => r.Certificate)
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
            .ToListAsync();

        var result = new BatchCertificateIssueResultDto
        {
            EventId = eventId,
            TotalEligible = eligibleRegistrations.Count(r => r.Attendance?.Status == AttendanceStatus.CheckedIn &&
                                                             (ev.EventType != EventType.Paid || r.HasSuccessfulPayment))
        };

        foreach (var reg in eligibleRegistrations)
        {
            if (reg.Certificate != null)
            {
                result.SkippedCount++;
                continue;
            }

            if (reg.Attendance?.Status != AttendanceStatus.CheckedIn)
            {
                result.SkippedCount++;
                continue;
            }

            if (ev.EventType == EventType.Paid && !reg.HasSuccessfulPayment)
            {
                result.SkippedCount++;
                continue;
            }

            var issueRes = await IssueCertificateAsync(reg.Id, issuedByUserId);
            if (issueRes.Success)
            {
                result.IssuedCount++;
            }
            else
            {
                result.FailedCount++;
                result.Messages.Add($"Reg #{reg.Id}: {issueRes.Message}");
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CertificateDetailDto?> GetCertificateByIdAsync(int certificateId, string? requestingUserId = null, bool isStaff = false)
    {
        var cert = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.Event)
                .ThenInclude(e => e.Department)
            .Include(c => c.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(c => c.Event)
                .ThenInclude(e => e.Coordinators)
            .Include(c => c.Event)
                .ThenInclude(e => e.CertificateTemplate)
            .Include(c => c.User)
            .Include(c => c.Registration)
            .Include(c => c.IssuedByUser)
            .Include(c => c.RevokedByUser)
            .FirstOrDefaultAsync(c => c.Id == certificateId);

        if (cert == null) return null;

        // Tenant and authorization enforcement
        if (!isStaff)
        {
            if (!string.IsNullOrEmpty(requestingUserId) && cert.UserId != requestingUserId)
            {
                throw new UnauthorizedAccessException("Access denied: You do not own this certificate.");
            }
        }
        else
        {
            // If coordinator, verify assignment to event
            if (!string.IsNullOrEmpty(requestingUserId))
            {
                var isAdmin = await _context.UserRoles.AnyAsync(ur => ur.UserId == requestingUserId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && (r.Name == nameof(UserRole.Admin) || r.Name == nameof(UserRole.SuperAdmin))));

                if (!isAdmin && !cert.Event.Coordinators.Any(c => c.UserId == requestingUserId))
                {
                    throw new UnauthorizedAccessException("Coordinators can only access certificates for their assigned events.");
                }
            }
        }

        return MapToDetailDto(cert);
    }

    /// <inheritdoc />
    public async Task<CertificateDetailDto?> GetCertificateByRegistrationIdAsync(int registrationId, string? requestingUserId = null, bool isStaff = false)
    {
        var cert = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.Event)
                .ThenInclude(e => e.Department)
            .Include(c => c.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(c => c.Event)
                .ThenInclude(e => e.Coordinators)
            .Include(c => c.Event)
                .ThenInclude(e => e.CertificateTemplate)
            .Include(c => c.User)
            .Include(c => c.Registration)
            .Include(c => c.IssuedByUser)
            .Include(c => c.RevokedByUser)
            .FirstOrDefaultAsync(c => c.RegistrationId == registrationId);

        if (cert == null) return null;

        if (!isStaff && !string.IsNullOrEmpty(requestingUserId) && cert.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("Access denied: You do not own this certificate.");
        }

        return MapToDetailDto(cert);
    }

    /// <inheritdoc />
    public async Task<CertificateVerificationDto> VerifyCertificateAsync(string searchIdentifier)
    {
        if (string.IsNullOrWhiteSpace(searchIdentifier))
        {
            return new CertificateVerificationDto
            {
                IsValid = false,
                Message = "Please enter a certificate number or verification token."
            };
        }

        var clean = searchIdentifier.Trim();

        var cert = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.Event)
                .ThenInclude(e => e.Department)
            .Include(c => c.Event)
                .ThenInclude(e => e.CertificateTemplate)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CertificateNumber == clean || c.VerificationToken == clean);

        if (cert == null)
        {
            return new CertificateVerificationDto
            {
                IsValid = false,
                Message = "No certificate matching the provided identification code was found."
            };
        }

        var signatoryName = cert.Event?.CertificateTemplate?.SignatoryName;
        var signatoryTitle = cert.Event?.CertificateTemplate?.SignatoryTitle;
        if (string.IsNullOrWhiteSpace(signatoryName)) signatoryName = "Dr. K. Sharma";
        if (string.IsNullOrWhiteSpace(signatoryTitle)) signatoryTitle = "Dean of Academic Affairs";

        var dto = new CertificateVerificationDto
        {
            Status = cert.Status,
            CertificateNumber = cert.CertificateNumber,
            RecipientName = MaskName(cert.User?.FullName ?? "Student"),
            RecipientRollNumber = MaskRollNumber(cert.User?.RollNumber),
            EventTitle = cert.Event?.Title ?? "Event",
            DepartmentName = cert.Event?.Department?.Name ?? "VAP University",
            EventStartDate = cert.Event?.EventStartDate ?? DateTime.UtcNow,
            EventEndDate = cert.Event?.EventEndDate ?? DateTime.UtcNow,
            IssuedAt = cert.IssuedAt,
            SignatoryName = signatoryName,
            SignatoryTitle = signatoryTitle,
            RevocationReason = cert.RevocationReason,
            RevokedAt = cert.RevokedAt
        };

        if (cert.Status == CertificateStatus.Revoked)
        {
            dto.IsValid = false;
            dto.Message = $"Certificate was REVOKED on {cert.RevokedAt:dd MMM yyyy}. Reason: {cert.RevocationReason}";
        }
        else
        {
            dto.IsValid = true;
            dto.Message = "This certificate is authentic, officially recognized, and issued by VAP UNIVERSITY.";
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<(byte[] FileBytes, string FileName)?> DownloadCertificatePdfAsync(int certificateId, string? requestingUserId = null, bool isStaff = false)
    {
        var detail = await GetCertificateByIdAsync(certificateId, requestingUserId, isStaff);
        if (detail == null) return null;

        var webRoot = GetWebRootPath();
        var relativePath = detail.PdfFilePath.Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(webRoot, relativePath);

        // Security: Prevent Directory Traversal
        var canonicalWebRoot = Path.GetFullPath(webRoot);
        var canonicalFilePath = Path.GetFullPath(physicalPath);
        if (!canonicalFilePath.StartsWith(canonicalWebRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Directory traversal attack detected.");
        }

        byte[] bytes;
        if (File.Exists(physicalPath))
        {
            bytes = await File.ReadAllBytesAsync(physicalPath);
        }
        else
        {
            // Dynamically regenerate PDF if missing from disk
            var verificationUrl = $"{GetBaseUrl()}/Certificates/Verify/{detail.VerificationToken}";
            bytes = _pdfGenerator.GenerateCertificatePdf(detail, verificationUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
            await File.WriteAllBytesAsync(physicalPath, bytes);
        }

        return (bytes, $"{detail.CertificateNumber}.pdf");
    }

    /// <inheritdoc />
    public async Task<CertificateResultDto> RevokeCertificateAsync(int certificateId, string revocationReason, string revokedByUserId)
    {
        if (string.IsNullOrWhiteSpace(revocationReason))
        {
            return CertificateResultDto.Fail(CertificateStatusCode.RevocationReasonRequired, "A non-empty revocation reason is required.");
        }

        var cert = await _context.Certificates
            .Include(c => c.Event)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == certificateId);

        if (cert == null)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.CertificateNotFound, "Certificate not found.");
        }

        if (cert.Status == CertificateStatus.Revoked)
        {
            return CertificateResultDto.Fail(CertificateStatusCode.Revoked, "Certificate is already revoked.");
        }

        cert.Status = CertificateStatus.Revoked;
        cert.RevocationReason = revocationReason.Trim();
        cert.RevokedAt = DateTime.UtcNow;
        cert.RevokedByUserId = revokedByUserId;
        cert.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit Log
        await _auditLogService.LogAsync(
            action: "RevokeCertificate",
            entityName: nameof(Certificate),
            entityId: cert.Id.ToString(),
            userId: revokedByUserId,
            newValues: $"Revoked certificate '{cert.CertificateNumber}' for Student {cert.User?.FullName}. Reason: {cert.RevocationReason}");

        return CertificateResultDto.Ok(
            cert.Id,
            cert.CertificateNumber,
            cert.VerificationToken,
            cert.PdfFilePath,
            "Certificate has been officially revoked.");
    }

    /// <inheritdoc />
    public async Task<(List<CertificateDetailDto> Items, int TotalCount)> GetCertificatesLedgerAsync(
        CertificateFilterCriteria criteria,
        string? staffUserId = null,
        bool isCoordinator = false)
    {
        var query = _context.Certificates
            .AsNoTracking()
            .Include(c => c.Event)
                .ThenInclude(e => e.Department)
            .Include(c => c.Event)
                .ThenInclude(e => e.EventCategory)
            .Include(c => c.Event)
                .ThenInclude(e => e.Coordinators)
            .Include(c => c.Event)
                .ThenInclude(e => e.CertificateTemplate)
            .Include(c => c.User)
            .Include(c => c.Registration)
            .Include(c => c.IssuedByUser)
            .Include(c => c.RevokedByUser)
            .AsQueryable();

        // Scoping for Coordinator
        if (isCoordinator && !string.IsNullOrEmpty(staffUserId))
        {
            query = query.Where(c => c.Event.Coordinators.Any(ec => ec.UserId == staffUserId));
        }

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(c => c.EventId == criteria.EventId.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(c => c.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(c => c.Status == criteria.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(c => c.CertificateNumber.ToLower().Contains(term) ||
                                     c.User.FirstName.ToLower().Contains(term) ||
                                     c.User.LastName.ToLower().Contains(term) ||
                                     (c.User.RollNumber != null && c.User.RollNumber.ToLower().Contains(term)) ||
                                     c.Event.Title.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 20 : criteria.PageSize;

        var items = await query
            .OrderByDescending(c => c.IssuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items.Select(MapToDetailDto).ToList(), total);
    }

    /// <inheritdoc />
    public async Task<CertificateTemplate?> GetTemplateForEventAsync(int eventId)
    {
        return await _context.CertificateTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(ct => ct.EventId == eventId);
    }

    /// <inheritdoc />
    public async Task<bool> UpsertTemplateAsync(CertificateTemplateUpsertDto dto, string modifiedByUserId)
    {
        var template = await _context.CertificateTemplates.FirstOrDefaultAsync(ct => ct.EventId == dto.EventId);
        if (template == null)
        {
            template = new CertificateTemplate
            {
                EventId = dto.EventId,
                TemplateTitle = dto.TemplateTitle.Trim(),
                SignatoryName = dto.SignatoryName.Trim(),
                SignatoryTitle = dto.SignatoryTitle.Trim(),
                SignatureImagePath = dto.SignatureImagePath,
                BackgroundImagePath = dto.BackgroundImagePath,
                CertificateCodePrefix = string.IsNullOrWhiteSpace(dto.CertificateCodePrefix) ? "CERT" : dto.CertificateCodePrefix.Trim().ToUpperInvariant(),
                IssueDate = DateTime.UtcNow
            };
            _context.CertificateTemplates.Add(template);
        }
        else
        {
            template.TemplateTitle = dto.TemplateTitle.Trim();
            template.SignatoryName = dto.SignatoryName.Trim();
            template.SignatoryTitle = dto.SignatoryTitle.Trim();
            template.SignatureImagePath = dto.SignatureImagePath;
            template.BackgroundImagePath = dto.BackgroundImagePath;
            template.CertificateCodePrefix = string.IsNullOrWhiteSpace(dto.CertificateCodePrefix) ? "CERT" : dto.CertificateCodePrefix.Trim().ToUpperInvariant();
            template.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "UpsertCertificateTemplate",
            entityName: nameof(CertificateTemplate),
            entityId: template.Id.ToString(),
            userId: modifiedByUserId,
            newValues: $"Updated certificate template for event #{dto.EventId}.");

        return true;
    }

    private static CertificateDetailDto MapToDetailDto(Certificate cert)
    {
        var signatoryName = cert.Event?.CertificateTemplate?.SignatoryName;
        var signatoryTitle = cert.Event?.CertificateTemplate?.SignatoryTitle;
        if (string.IsNullOrWhiteSpace(signatoryName)) signatoryName = "Dr. K. Sharma";
        if (string.IsNullOrWhiteSpace(signatoryTitle)) signatoryTitle = "Dean of Academic Affairs";

        return new CertificateDetailDto
        {
            Id = cert.Id,
            RegistrationId = cert.RegistrationId,
            RegistrationNumber = cert.Registration?.RegistrationNumber ?? string.Empty,
            EventId = cert.EventId,
            EventTitle = cert.Event?.Title ?? string.Empty,
            EventCategoryName = cert.Event?.EventCategory?.Name ?? "General",
            EventDepartmentName = cert.Event?.Department?.Name ?? "VAP University",
            EventStartDate = cert.Event?.EventStartDate ?? DateTime.MinValue,
            EventEndDate = cert.Event?.EventEndDate ?? DateTime.MinValue,
            UserId = cert.UserId,
            StudentFullName = cert.User?.FullName ?? string.Empty,
            StudentRollNumber = cert.User?.RollNumber,
            StudentDepartment = cert.User?.Department,
            StudentCollegeName = cert.User?.CollegeName,
            CertificateNumber = cert.CertificateNumber,
            VerificationToken = cert.VerificationToken,
            PdfFilePath = cert.PdfFilePath,
            IssuedAt = cert.IssuedAt,
            Status = cert.Status,
            RevocationReason = cert.RevocationReason,
            RevokedAt = cert.RevokedAt,
            IssuedByUserName = cert.IssuedByUser?.FullName,
            RevokedByUserName = cert.RevokedByUser?.FullName,
            SignatoryName = signatoryName,
            SignatoryTitle = signatoryTitle
        };
    }

    private static string MaskName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "Student";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => p.Length <= 2 ? p : $"{p[0]}{new string('*', p.Length - 2)}{p[^1]}"));
    }

    private static string? MaskRollNumber(string? rollNumber)
    {
        if (string.IsNullOrWhiteSpace(rollNumber)) return null;
        var r = rollNumber.Trim();
        if (r.Length <= 3) return new string('*', r.Length);
        return $"{r[..2]}{new string('*', r.Length - 4)}{r[^2..]}";
    }

    private static string GenerateSecureCode(int length)
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        return RandomNumberGenerator.GetString(chars, length);
    }

    private string GetWebRootPath()
    {
        if (!string.IsNullOrEmpty(_environment.WebRootPath))
        {
            return _environment.WebRootPath;
        }

        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (!Directory.Exists(candidate))
        {
            Directory.CreateDirectory(candidate);
        }
        return candidate;
    }

    private string GetBaseUrl()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request != null)
        {
            return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        }
        return "https://localhost:7142";
    }
}

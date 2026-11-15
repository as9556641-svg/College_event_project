using System;
using System.Collections.Generic;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Status codes representing outcomes of certificate issuance, verification, and revocation operations.
/// </summary>
public enum CertificateStatusCode
{
    Success = 1,
    RegistrationNotFound = 2,
    UnauthorizedStudent = 3,
    RegistrationNotConfirmed = 4,
    AttendanceRequired = 5,
    EventNotEnded = 6,
    PaymentRequired = 7,
    AlreadyIssued = 8,
    CertificateNotFound = 9,
    UnauthorizedStaff = 10,
    RevocationReasonRequired = 11,
    EventNotFound = 12,
    TemplateNotFound = 13,
    Revoked = 14
}

/// <summary>
/// Parameters for issuing a certificate.
/// </summary>
public class CertificateIssueRequestDto
{
    public int RegistrationId { get; set; }
    public string? IssuedByUserId { get; set; }
}

/// <summary>
/// Standard result returned by certificate operations.
/// </summary>
public class CertificateResultDto
{
    public bool Success { get; set; }
    public CertificateStatusCode StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CertificateId { get; set; }
    public string? CertificateNumber { get; set; }
    public string? VerificationToken { get; set; }
    public string? PdfFilePath { get; set; }

    public static CertificateResultDto Ok(int certificateId, string certificateNumber, string verificationToken, string pdfFilePath, string message = "Certificate issued successfully.") =>
        new()
        {
            Success = true,
            StatusCode = CertificateStatusCode.Success,
            Message = message,
            CertificateId = certificateId,
            CertificateNumber = certificateNumber,
            VerificationToken = verificationToken,
            PdfFilePath = pdfFilePath
        };

    public static CertificateResultDto Fail(CertificateStatusCode statusCode, string message) =>
        new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = message
        };
}

/// <summary>
/// Detailed view model for an issued certificate.
/// </summary>
public class CertificateDetailDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventCategoryName { get; set; } = string.Empty;
    public string EventDepartmentName { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string StudentFullName { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public string? StudentCollegeName { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public string PdfFilePath { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public CertificateStatus Status { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IssuedByUserName { get; set; }
    public string? RevokedByUserName { get; set; }
    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryTitle { get; set; } = string.Empty;
}

/// <summary>
/// Payload returned by the public certificate verification portal.
/// </summary>
public class CertificateVerificationDto
{
    public bool IsValid { get; set; }
    public CertificateStatus Status { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientRollNumber { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public DateTime IssuedAt { get; set; }
    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryTitle { get; set; } = string.Empty;
    public string? RevocationReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Eligibility status evaluation for a student registration.
/// </summary>
public class CertificateEligibilityDto
{
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventEndDate { get; set; }
    public bool IsEligible { get; set; }
    public string EligibilityReason { get; set; } = string.Empty;
    public bool IsIssued { get; set; }
    public int? CertificateId { get; set; }
    public string? CertificateNumber { get; set; }
    public string? VerificationToken { get; set; }
    public CertificateStatus? Status { get; set; }
}

/// <summary>
/// Search and filter criteria for certificates ledger.
/// </summary>
public class CertificateFilterCriteria
{
    public int? EventId { get; set; }
    public int? DepartmentId { get; set; }
    public string? SearchTerm { get; set; }
    public CertificateStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Result of batch certificate generation for an event.
/// </summary>
public class BatchCertificateIssueResultDto
{
    public int EventId { get; set; }
    public int TotalEligible { get; set; }
    public int IssuedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Request to upsert a certificate template configuration.
/// </summary>
public class CertificateTemplateUpsertDto
{
    public int EventId { get; set; }
    public string TemplateTitle { get; set; } = string.Empty;
    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryTitle { get; set; } = string.Empty;
    public string? SignatureImagePath { get; set; }
    public string? BackgroundImagePath { get; set; }
    public string CertificateCodePrefix { get; set; } = "CERT";
}

using System.Collections.Generic;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for certificate eligibility evaluation, automated issuance, revocation, and verification.
/// </summary>
public interface ICertificateService
{
    /// <summary>
    /// Checks whether a given registration is eligible for a certificate.
    /// </summary>
    Task<CertificateEligibilityDto> CheckEligibilityAsync(int registrationId, string? requestingUserId = null);

    /// <summary>
    /// Retrieves all certificate opportunities and issued certificates for a student.
    /// </summary>
    Task<List<CertificateEligibilityDto>> GetStudentCertificatesAsync(string studentUserId);

    /// <summary>
    /// Issues a completion certificate for an eligible registration.
    /// </summary>
    Task<CertificateResultDto> IssueCertificateAsync(int registrationId, string? issuedByUserId = null);

    /// <summary>
    /// Batch issues certificates for all eligible attendees of a completed event.
    /// </summary>
    Task<BatchCertificateIssueResultDto> BatchIssueCertificatesForEventAsync(int eventId, string issuedByUserId);

    /// <summary>
    /// Retrieves detailed certificate information by ID, enforcing student ownership or staff authorization.
    /// </summary>
    Task<CertificateDetailDto?> GetCertificateByIdAsync(int certificateId, string? requestingUserId = null, bool isStaff = false);

    /// <summary>
    /// Retrieves certificate details by registration ID.
    /// </summary>
    Task<CertificateDetailDto?> GetCertificateByRegistrationIdAsync(int registrationId, string? requestingUserId = null, bool isStaff = false);

    /// <summary>
    /// Public verification of a certificate by certificate number or cryptographic token.
    /// </summary>
    Task<CertificateVerificationDto> VerifyCertificateAsync(string searchIdentifier);

    /// <summary>
    /// Downloads the PDF bytes of an issued certificate with security traversal checks.
    /// </summary>
    Task<(byte[] FileBytes, string FileName)?> DownloadCertificatePdfAsync(int certificateId, string? requestingUserId = null, bool isStaff = false);

    /// <summary>
    /// Revokes an issued certificate with a mandatory reason, updating status and logging audit.
    /// </summary>
    Task<CertificateResultDto> RevokeCertificateAsync(int certificateId, string revocationReason, string revokedByUserId);

    /// <summary>
    /// Retrieves a paginated ledger of certificates, filtered by criteria and optionally scoped to coordinator-assigned events.
    /// </summary>
    Task<(List<CertificateDetailDto> Items, int TotalCount)> GetCertificatesLedgerAsync(CertificateFilterCriteria criteria, string? staffUserId = null, bool isCoordinator = false);

    /// <summary>
    /// Retrieves the certificate template for an event.
    /// </summary>
    Task<CertificateTemplate?> GetTemplateForEventAsync(int eventId);

    /// <summary>
    /// Creates or updates the certificate template configuration for an event.
    /// </summary>
    Task<bool> UpsertTemplateAsync(CertificateTemplateUpsertDto dto, string modifiedByUserId);
}

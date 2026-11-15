using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Contract for rendering A4 landscape university completion certificates to PDF bytes.
/// </summary>
public interface IPdfCertificateGenerator
{
    /// <summary>
    /// Generates high-fidelity A4 landscape PDF certificate bytes based on certificate details and verification URL.
    /// </summary>
    byte[] GenerateCertificatePdf(CertificateDetailDto model, string verificationUrl);
}

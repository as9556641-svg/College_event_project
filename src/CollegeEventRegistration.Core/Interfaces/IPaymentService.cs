using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for orchestrating the complete payment lifecycle for paid event registrations,
/// ensuring idempotent transaction processing, cryptographic validation, duplicate prevention,
/// and strict tenant & coordinator scoping.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Validates student ownership and registration status, generates a gateway order,
    /// and logs a pending payment record.
    /// </summary>
    Task<PaymentOrderResultDto> InitiatePaymentOrderAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Independently verifies the gateway signature, validates payment-to-registration association,
    /// confirms registration status, allocates seat capacity, and marks payment successful.
    /// </summary>
    Task<PaymentVerificationResultDto> VerifyAndConfirmPaymentAsync(
        VerifyPaymentRequestDto request,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and idempotently processes an incoming asynchronous webhook from the payment gateway.
    /// </summary>
    Task<(bool Success, string Message)> ProcessWebhookAsync(
        string rawBody,
        string signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated payment transactions for an authenticated student with strict tenant isolation.
    /// </summary>
    Task<PagedResult<PaymentSummaryDto>> GetStudentPaymentHistoryAsync(
        string studentUserId,
        StudentPaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed payment record for a student, strictly enforcing student ownership.
    /// </summary>
    Task<PaymentSummaryDto?> GetPaymentDetailsForStudentAsync(
        int paymentId,
        string studentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed payment record for administrative or coordinator inspection,
    /// enforcing coordinator event assignment scoping.
    /// </summary>
    Task<PaymentSummaryDto?> GetPaymentDetailsForStaffAsync(
        int paymentId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, searchable, and filterable list of institutional payment records for SuperAdmin/Admin.
    /// </summary>
    Task<PagedResult<PaymentSummaryDto>> GetPaymentsForAdminAsync(
        PaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of payments for an event assigned to a specific coordinator,
    /// strictly enforcing coordinator authorization.
    /// </summary>
    Task<PagedResult<PaymentSummaryDto>> GetPaymentsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        PaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed or cancelled checkout attempt for a registration.
    /// </summary>
    Task<bool> RecordPaymentFailureAsync(
        int registrationId,
        string orderId,
        string? paymentId,
        string reason,
        string studentUserId,
        CancellationToken cancellationToken = default);
}

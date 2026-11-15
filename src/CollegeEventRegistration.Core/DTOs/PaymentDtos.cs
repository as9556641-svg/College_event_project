using System.Collections.Generic;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

/// <summary>
/// Gateway configuration settings injected from ASP.NET Core User Secrets or environment.
/// </summary>
public class RazorpayOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool IsTestMode { get; set; } = true;
}

/// <summary>
/// Result returned by the gateway client upon order initialization.
/// </summary>
public class GatewayOrderResult
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public decimal Amount { get; set; }
    public long AmountInPaise { get; set; }
    public string Currency { get; set; } = "INR";
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Order payload returned to student checkout view.
/// </summary>
public class PaymentOrderResultDto
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public int PaymentRecordId { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long AmountInPaise => (long)Math.Round(Amount * 100);
    public string Currency { get; set; } = "INR";
    public string? KeyId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentEmail { get; set; }
    public string? StudentContact { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Verification request submitted after completion of checkout.
/// </summary>
public class VerifyPaymentRequestDto
{
    public int RegistrationId { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
}

/// <summary>
/// Result of cryptographic signature and database verification.
/// </summary>
public class PaymentVerificationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? PaymentId { get; set; }
    public string? TransactionId { get; set; }
    public string? OrderId { get; set; }
    public PaymentStatus Status { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public int RegistrationId { get; set; }
}

/// <summary>
/// Summary representation of a payment transaction for tables and student receipts.
/// </summary>
public class PaymentSummaryDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string? EventDepartmentCode { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentRollNumber { get; set; }
    public string? StudentDepartment { get; set; }
    public string? StudentCollegeName { get; set; }

    public string? OrderId { get; set; }
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// Filter criteria for querying payments in Admin and Coordinator portals.
/// </summary>
public class PaymentFilterCriteria
{
    public int? EventId { get; set; }
    public int? DepartmentId { get; set; }
    public PaymentStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>
/// Filter criteria for querying payments in Student portal.
/// </summary>
public class StudentPaymentFilterCriteria
{
    public PaymentStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

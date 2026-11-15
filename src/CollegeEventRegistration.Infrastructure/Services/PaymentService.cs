using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Exceptions;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing Phase 8 Payment Integration.
/// Enforces idempotent transaction processing, independent cryptographic signature validation,
/// duplicate payment prevention, capacity allocation on payment confirmation,
/// strict student tenant isolation, and coordinator event scoping.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentGatewayClient _gatewayClient;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PaymentService> _logger;
    private readonly INotificationService? _notificationService;

    public PaymentService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentGatewayClient gatewayClient,
        IAuditLogService auditLogService,
        ILogger<PaymentService> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _userManager = userManager;
        _gatewayClient = gatewayClient;
        _auditLogService = auditLogService;
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task<PaymentOrderResultDto> InitiatePaymentOrderAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0)
        {
            return new PaymentOrderResultDto { Success = false, Message = "Invalid registration identifier." };
        }

        if (string.IsNullOrWhiteSpace(studentUserId))
        {
            return new PaymentOrderResultDto { Success = false, Message = "Authentication is required to initiate payment." };
        }

        var reg = await _context.EventRegistrations
            .Include(r => r.Event)
            .Include(r => r.User)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == registrationId && !r.Event.IsDeleted, cancellationToken);

        if (reg == null)
        {
            return new PaymentOrderResultDto { Success = false, Message = "Registration record not found." };
        }

        // STRICT Student Tenant Isolation Guard
        if (reg.UserId != studentUserId)
        {
            _logger.LogWarning("Security breach attempt: Student {UserId} attempted to initiate payment for registration #{RegId} owned by {OwnerId}",
                studentUserId, registrationId, reg.UserId);
            return new PaymentOrderResultDto { Success = false, Message = "You are not authorized to pay for this registration." };
        }

        if (reg.Status == RegistrationStatus.Cancelled)
        {
            return new PaymentOrderResultDto { Success = false, Message = "Cannot pay for a cancelled registration." };
        }

        // Validate event is Paid
        if (reg.Event.EventType == EventType.Free || reg.Event.RegistrationFee <= 0)
        {
            return new PaymentOrderResultDto { Success = false, Message = "This event is free; no payment is required." };
        }

        // DUPLICATE PAYMENT GUARD: Check if already paid
        var existingSuccess = reg.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Success);
        if (existingSuccess != null)
        {
            return new PaymentOrderResultDto
            {
                Success = false,
                RegistrationId = registrationId,
                RegistrationNumber = reg.RegistrationNumber,
                EventTitle = reg.Event.Title,
                Amount = reg.Event.RegistrationFee,
                Message = $"Registration #{reg.RegistrationNumber} is already paid (Transaction: {existingSuccess.TransactionId})."
            };
        }

        // Capacity check
        if (reg.Event.RegisteredCount >= reg.Event.TotalCapacity && reg.Status != RegistrationStatus.Confirmed)
        {
            return new PaymentOrderResultDto
            {
                Success = false,
                Message = $"Event has reached maximum seat capacity ({reg.Event.TotalCapacity} attendees)."
            };
        }

        // Create order via gateway client
        var notes = new Dictionary<string, string>
        {
            ["registrationId"] = reg.Id.ToString(),
            ["registrationNumber"] = reg.RegistrationNumber,
            ["eventId"] = reg.EventId.ToString(),
            ["studentUserId"] = studentUserId,
            ["studentEmail"] = reg.User?.Email ?? string.Empty
        };

        var orderResult = await _gatewayClient.CreateOrderAsync(
            reg.Event.RegistrationFee,
            "INR",
            reg.RegistrationNumber,
            notes,
            cancellationToken);

        if (!orderResult.Success || string.IsNullOrEmpty(orderResult.OrderId))
        {
            return new PaymentOrderResultDto
            {
                Success = false,
                Message = orderResult.ErrorMessage ?? "Failed to initialize gateway order."
            };
        }

        // Persist pending payment entity in database
        var payment = new Payment
        {
            RegistrationId = reg.Id,
            OrderId = orderResult.OrderId,
            Amount = reg.Event.RegistrationFee,
            Currency = "INR",
            Status = PaymentStatus.Pending,
            InitiatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            action: "InitiatePayment",
            entityName: "Payment",
            entityId: payment.Id.ToString(),
            userId: studentUserId,
            newValues: $"OrderId: {orderResult.OrderId}, Amount: {payment.Amount}, RegId: {reg.Id}"
        );

        _logger.LogInformation("Initiated payment order {OrderId} for Registration #{RegNum} (ID: {RegId}), Amount: INR {Amount}",
            orderResult.OrderId, reg.RegistrationNumber, reg.Id, payment.Amount);

        return new PaymentOrderResultDto
        {
            Success = true,
            OrderId = orderResult.OrderId,
            PaymentRecordId = payment.Id,
            RegistrationId = reg.Id,
            RegistrationNumber = reg.RegistrationNumber,
            EventId = reg.EventId,
            EventTitle = reg.Event.Title,
            Amount = reg.Event.RegistrationFee,
            Currency = "INR",
            KeyId = _gatewayClient.KeyId,
            StudentName = reg.User?.FullName,
            StudentEmail = reg.User?.Email,
            StudentContact = reg.User?.PhoneNumber
        };
    }

    /// <inheritdoc />
    public async Task<PaymentVerificationResultDto> VerifyAndConfirmPaymentAsync(
        VerifyPaymentRequestDto request,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.PaymentId))
        {
            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = "Invalid payment verification payload."
            };
        }

        // 1. Initial lookup and Tenant Isolation Check
        var payment = await _context.Payments
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, cancellationToken);

        if (payment == null)
        {
            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = $"Payment record for Order '{request.OrderId}' was not found in system."
            };
        }

        var reg = payment.Registration;

        // STRICT Student Tenant Isolation Check
        if (reg.UserId != studentUserId)
        {
            _logger.LogWarning("Tenant violation attempt: User {UserId} tried verifying payment for registration of {OwnerId}",
                studentUserId, reg.UserId);
            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = "You are not authorized to verify this payment."
            };
        }

        // IDEMPOTENCY GUARD: If already marked Success for this TransactionId, return clean success
        if (payment.Status == PaymentStatus.Success)
        {
            if (payment.TransactionId == request.PaymentId)
            {
                return new PaymentVerificationResultDto
                {
                    Success = true,
                    PaymentId = payment.Id,
                    TransactionId = payment.TransactionId,
                    OrderId = payment.OrderId,
                    Status = PaymentStatus.Success,
                    RegistrationStatus = reg.Status,
                    RegistrationId = reg.Id,
                    Message = "Payment was already verified and confirmed."
                };
            }

            return new PaymentVerificationResultDto
            {
                Success = false,
                Message = "A successful payment is already associated with this registration."
            };
        }

        // 2. INDEPENDENT CRYPTOGRAPHIC SIGNATURE VERIFICATION
        var isSignatureValid = _gatewayClient.VerifyPaymentSignature(
            request.OrderId,
            request.PaymentId,
            request.Signature);

        if (!isSignatureValid)
        {
            _logger.LogWarning("Cryptographic payment signature mismatch for Order {OrderId}, Payment {PaymentId}",
                request.OrderId, request.PaymentId);

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Cryptographic gateway signature verification failed.";
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new PaymentVerificationResultDto
            {
                Success = false,
                OrderId = request.OrderId,
                TransactionId = request.PaymentId,
                Status = PaymentStatus.Failed,
                RegistrationStatus = RegistrationStatus.Pending,
                Message = "Payment signature verification failed. Transaction was not confirmed."
            };
        }

        // 3. ATOMIC TRANSACTIONAL SEAT ALLOCATION & PAYMENT PERSISTENCE
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Re-fetch inside transaction for concurrency safety
                var currentPayment = await _context.Payments
                    .Include(p => p.Registration)
                        .ThenInclude(r => r.Event)
                    .Include(p => p.Registration)
                        .ThenInclude(r => r.User)
                    .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, cancellationToken);

                if (currentPayment == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new PaymentVerificationResultDto
                    {
                        Success = false,
                        Message = $"Payment record for Order '{request.OrderId}' was not found in system."
                    };
                }

                var currentReg = currentPayment.Registration;

                if (currentPayment.Status == PaymentStatus.Success && currentPayment.TransactionId == request.PaymentId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new PaymentVerificationResultDto
                    {
                        Success = true,
                        PaymentId = currentPayment.Id,
                        TransactionId = currentPayment.TransactionId,
                        OrderId = currentPayment.OrderId,
                        Status = PaymentStatus.Success,
                        RegistrationStatus = currentReg.Status,
                        RegistrationId = currentReg.Id,
                        Message = "Payment was already verified and confirmed."
                    };
                }

                // DUPLICATE TRANSACTION REPLAY PROTECTION
                var isDuplicateTxn = await _context.Payments
                    .AnyAsync(p => p.TransactionId == request.PaymentId && p.Id != currentPayment.Id, cancellationToken);

                if (isDuplicateTxn)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("Replay attack detected: Transaction ID {PaymentId} has already been used on another payment.", request.PaymentId);
                    return new PaymentVerificationResultDto
                    {
                        Success = false,
                        Message = "This payment transaction ID has already been processed."
                    };
                }

                // AMOUNT AND CURRENCY VERIFICATION
                if (currentPayment.Amount != currentReg.Event.RegistrationFee)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    currentPayment.Status = PaymentStatus.Failed;
                    currentPayment.FailureReason = $"Amount mismatch: paid {currentPayment.Amount}, required {currentReg.Event.RegistrationFee}";
                    await _context.SaveChangesAsync(cancellationToken);
                    return new PaymentVerificationResultDto
                    {
                        Success = false,
                        Message = "Payment amount does not match the configured event fee."
                    };
                }

                // CAPACITY CHECK & INCREMENT
                if (currentReg.Status != RegistrationStatus.Confirmed)
                {
                    if (currentReg.Event.RegisteredCount >= currentReg.Event.TotalCapacity)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _logger.LogWarning("Event {EventId} reached capacity when confirming payment for Reg #{RegId}",
                            currentReg.EventId, currentReg.Id);
                        return new PaymentVerificationResultDto
                        {
                            Success = false,
                            Message = "Event has reached maximum seat capacity. Please contact campus administrator."
                        };
                    }

                    currentReg.Event.RegisteredCount++;
                    currentReg.Event.UpdatedAt = DateTime.UtcNow;
                }

                // Update payment record
                currentPayment.Status = PaymentStatus.Success;
                currentPayment.TransactionId = request.PaymentId;
                currentPayment.PaymentMethod = request.PaymentMethod ?? "Razorpay";
                currentPayment.CompletedAt = DateTime.UtcNow;
                currentPayment.UpdatedAt = DateTime.UtcNow;

                // Transition registration to Confirmed
                currentReg.Status = RegistrationStatus.Confirmed;
                currentReg.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Audit log successful payment
                await _auditLogService.LogAsync(
                    action: "PaymentSuccess",
                    entityName: "Payment",
                    entityId: currentPayment.Id.ToString(),
                    userId: studentUserId,
                    newValues: $"TxnId: {currentPayment.TransactionId}, OrderId: {currentPayment.OrderId}, Amount: {currentPayment.Amount}, RegStatus: {currentReg.Status}"
                );

                _logger.LogInformation("Payment {PaymentId} (Order: {OrderId}) confirmed successfully for Registration #{RegNum}. Seat count: {Count}/{Total}",
                    request.PaymentId, request.OrderId, currentReg.RegistrationNumber, currentReg.Event.RegisteredCount, currentReg.Event.TotalCapacity);

                if (_notificationService != null)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = studentUserId,
                            EventId = currentReg.EventId,
                            Title = "Payment Successful",
                            Message = $"Payment of INR {currentPayment.Amount:F2} verified for '{currentReg.Event.Title}'. Order #{currentPayment.OrderId}.",
                            Type = NotificationType.PaymentSuccess,
                            ActionUrl = $"/Student/Registrations/Details/{currentReg.Id}",
                            CorrelationId = $"PAY-SUCC-{currentPayment.Id}"
                        }, cancellationToken);
                    }
                    catch (Exception nEx)
                    {
                        _logger.LogWarning(nEx, "Failed to dispatch payment success notification for #{PaymentId}", currentPayment.Id);
                    }
                }

                return new PaymentVerificationResultDto
                {
                    Success = true,
                    PaymentId = currentPayment.Id,
                    TransactionId = currentPayment.TransactionId,
                    OrderId = currentPayment.OrderId,
                    Status = PaymentStatus.Success,
                    RegistrationStatus = currentReg.Status,
                    RegistrationId = currentReg.Id,
                    Message = $"Payment of INR {currentPayment.Amount:F2} confirmed successfully. Your registration #{currentReg.RegistrationNumber} is now Confirmed!"
                };
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Database update exception confirming payment for Order {OrderId}", request.OrderId);

                if (ex.InnerException?.Message.Contains("UX_Payments_Registration_Success") == true)
                {
                    return new PaymentVerificationResultDto
                    {
                        Success = false,
                        Message = "A successful payment is already associated with this registration."
                    };
                }

                if (ex.InnerException?.Message.Contains("UX_Payments_TransactionId") == true)
                {
                    return new PaymentVerificationResultDto
                    {
                        Success = false,
                        Message = "Duplicate transaction ID detected."
                    };
                }

                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Unexpected error confirming payment for Order {OrderId}", request.OrderId);
                return new PaymentVerificationResultDto
                {
                    Success = false,
                    Message = "An unexpected error occurred while confirming payment."
                };
            }
        });
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ProcessWebhookAsync(
        string rawBody,
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signature))
        {
            return (false, "Missing webhook body or signature.");
        }

        // Webhook signature verification
        var isValid = _gatewayClient.VerifyWebhookSignature(rawBody, signature);
        if (!isValid)
        {
            _logger.LogWarning("Webhook signature verification failed.");
            return (false, "Invalid webhook signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var eventName = root.TryGetProperty("event", out var evProp) ? evProp.GetString() : null;

            if (eventName != "payment.captured" && eventName != "order.paid")
            {
                // Acknowledge non-captured events gracefully
                return (true, $"Event '{eventName}' acknowledged (no action required).");
            }

            if (!root.TryGetProperty("payload", out var payloadProp) ||
                !payloadProp.TryGetProperty("payment", out var paymentProp) ||
                !paymentProp.TryGetProperty("entity", out var entityProp))
            {
                return (false, "Malformed webhook payload structure.");
            }

            var orderId = entityProp.TryGetProperty("order_id", out var orderIdProp) ? orderIdProp.GetString() : null;
            var paymentId = entityProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var method = entityProp.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : "webhook";

            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(paymentId))
            {
                return (false, "Missing order_id or payment_id in webhook entity.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                var payment = await _context.Payments
                    .Include(p => p.Registration)
                        .ThenInclude(r => r.Event)
                    .FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

                if (payment == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (false, $"Payment record for order {orderId} not found.");
                }

                // Idempotent guard
                if (payment.Status == PaymentStatus.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return (true, "Payment already recorded as successful (idempotent).");
                }

                payment.Status = PaymentStatus.Success;
                payment.TransactionId = paymentId;
                payment.PaymentMethod = method;
                payment.CompletedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                if (payment.Registration.Status != RegistrationStatus.Confirmed)
                {
                    if (payment.Registration.Event.RegisteredCount < payment.Registration.Event.TotalCapacity)
                    {
                        payment.Registration.Event.RegisteredCount++;
                        payment.Registration.Event.UpdatedAt = DateTime.UtcNow;
                    }

                    payment.Registration.Status = RegistrationStatus.Confirmed;
                    payment.Registration.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await _auditLogService.LogAsync(
                    action: "WebhookPaymentSuccess",
                    entityName: "Payment",
                    entityId: payment.Id.ToString(),
                    userId: payment.Registration.UserId,
                    newValues: $"TxnId: {paymentId}, OrderId: {orderId}, Event: {eventName}"
                );

                _logger.LogInformation("Webhook confirmed payment {PaymentId} for Order {OrderId}", paymentId, orderId);

                if (_notificationService != null)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = payment.Registration.UserId,
                            EventId = payment.Registration.EventId,
                            Title = "Payment Successful",
                            Message = $"Payment of INR {payment.Amount:F2} confirmed via webhook for '{eventName}'. Order #{payment.OrderId}.",
                            Type = NotificationType.PaymentSuccess,
                            ActionUrl = $"/Student/Registrations/Details/{payment.RegistrationId}",
                            CorrelationId = $"PAY-SUCC-{payment.Id}"
                        }, cancellationToken);
                    }
                    catch (Exception nEx)
                    {
                        _logger.LogWarning(nEx, "Failed to dispatch webhook payment notification for #{PaymentId}", payment.Id);
                    }
                }

                return (true, "Webhook successfully processed and payment confirmed.");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Razorpay webhook");
            return (false, "Webhook processing exception.");
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<PaymentSummaryDto>> GetStudentPaymentHistoryAsync(
        string studentUserId,
        StudentPaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new StudentPaymentFilterCriteria();

        // STRICT student tenant isolation: p.Registration.UserId == studentUserId
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .Where(p => p.Registration.UserId == studentUserId && !p.Registration.Event.IsDeleted);

        if (criteria.Status.HasValue)
        {
            query = query.Where(p => p.Status == criteria.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                (p.OrderId != null && p.OrderId.ToLower().Contains(term)) ||
                (p.TransactionId != null && p.TransactionId.ToLower().Contains(term)) ||
                p.Registration.RegistrationNumber.ToLower().Contains(term) ||
                p.Registration.Event.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 50);

        var items = await query
            .OrderByDescending(p => p.InitiatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PaymentSummaryDto?> GetPaymentDetailsForStudentAsync(
        int paymentId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && !p.Registration.Event.IsDeleted, cancellationToken);

        if (payment == null)
        {
            return null;
        }

        // STRICT Student Tenant Isolation
        if (payment.Registration.UserId != studentUserId)
        {
            _logger.LogWarning("Tenant violation attempt: User {UserId} tried accessing payment #{PaymentId} of {OwnerId}",
                studentUserId, paymentId, payment.Registration.UserId);
            return null;
        }

        return MapToSummaryDto(payment);
    }

    /// <inheritdoc />
    public async Task<PaymentSummaryDto?> GetPaymentDetailsForStaffAsync(
        int paymentId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId && !p.Registration.Event.IsDeleted, cancellationToken);

        if (payment == null)
        {
            return null;
        }

        // Coordinator Scoping Guard
        if (staffRole == nameof(UserRole.Coordinator))
        {
            var isCoordinating = await _context.Events
                .AnyAsync(e => e.Id == payment.Registration.EventId && !e.IsDeleted &&
                    (e.CreatedByUserId == staffUserId || e.Coordinators.Any(c => c.UserId == staffUserId)), cancellationToken);

            if (!isCoordinating)
            {
                _logger.LogWarning("Coordinator {UserId} denied access to payment #{PaymentId} for unassigned Event {EventId}",
                    staffUserId, paymentId, payment.Registration.EventId);
                return null;
            }
        }

        return MapToSummaryDto(payment);
    }

    /// <inheritdoc />
    public async Task<PagedResult<PaymentSummaryDto>> GetPaymentsForAdminAsync(
        PaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new PaymentFilterCriteria();

        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .Where(p => !p.Registration.Event.IsDeleted);

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(p => p.Registration.EventId == criteria.EventId.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(p => p.Registration.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(p => p.Status == criteria.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                (p.OrderId != null && p.OrderId.ToLower().Contains(term)) ||
                (p.TransactionId != null && p.TransactionId.ToLower().Contains(term)) ||
                p.Registration.RegistrationNumber.ToLower().Contains(term) ||
                p.Registration.Event.Title.ToLower().Contains(term) ||
                p.Registration.User.FirstName.ToLower().Contains(term) ||
                p.Registration.User.LastName.ToLower().Contains(term) ||
                (p.Registration.User.Email != null && p.Registration.User.Email.ToLower().Contains(term)) ||
                (p.Registration.User.RollNumber != null && p.Registration.User.RollNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(p => p.InitiatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<PaymentSummaryDto>> GetPaymentsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        PaymentFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0)
        {
            return new PagedResult<PaymentSummaryDto> { Items = new List<PaymentSummaryDto>() };
        }

        // STRICT Coordinator Scope Validation
        var isAssigned = await _context.Events
            .AnyAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.CreatedByUserId == coordinatorUserId || e.Coordinators.Any(c => c.UserId == coordinatorUserId)), cancellationToken);

        if (!isAssigned)
        {
            _logger.LogWarning("Coordinator {UserId} unauthorized access to payments for Event {EventId}",
                coordinatorUserId, eventId);
            throw new UnauthorizedAccessException($"Coordinator is not authorized to view payments for Event #{eventId}.");
        }

        criteria ??= new PaymentFilterCriteria();
        criteria.EventId = eventId;

        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(p => p.Registration)
                .ThenInclude(r => r.User)
            .Where(p => p.Registration.EventId == eventId && !p.Registration.Event.IsDeleted);

        if (criteria.Status.HasValue)
        {
            query = query.Where(p => p.Status == criteria.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                (p.OrderId != null && p.OrderId.ToLower().Contains(term)) ||
                (p.TransactionId != null && p.TransactionId.ToLower().Contains(term)) ||
                p.Registration.RegistrationNumber.ToLower().Contains(term) ||
                p.Registration.User.FirstName.ToLower().Contains(term) ||
                p.Registration.User.LastName.ToLower().Contains(term) ||
                (p.Registration.User.Email != null && p.Registration.User.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(p => p.InitiatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<bool> RecordPaymentFailureAsync(
        int registrationId,
        string orderId,
        string? paymentId,
        string reason,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _context.Payments
            .Include(p => p.Registration)
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.RegistrationId == registrationId, cancellationToken);

        if (payment == null)
        {
            return false;
        }

        // Strict ownership check
        if (payment.Registration.UserId != studentUserId)
        {
            return false;
        }

        // Don't overwrite an already successful payment
        if (payment.Status == PaymentStatus.Success)
        {
            return false;
        }

        payment.Status = PaymentStatus.Failed;
        payment.TransactionId = paymentId;
        payment.FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment was cancelled or failed at gateway." : reason.Trim();
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            action: "PaymentFailed",
            entityName: "Payment",
            entityId: payment.Id.ToString(),
            userId: studentUserId,
            newValues: $"Reason: {payment.FailureReason}, OrderId: {orderId}"
        );

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = studentUserId,
                    EventId = payment.Registration.EventId,
                    Title = "Payment Failed",
                    Message = $"Payment failed for '{payment.Registration.Event.Title}': {payment.FailureReason}",
                    Type = NotificationType.PaymentFailed,
                    ActionUrl = "/Student/Registrations",
                    CorrelationId = $"PAY-FAIL-{payment.Id}"
                }, cancellationToken);
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to dispatch payment failure notification for #{PaymentId}", payment.Id);
            }
        }

        return true;
    }

    private static PaymentSummaryDto MapToSummaryDto(Payment p)
    {
        return new PaymentSummaryDto
        {
            Id = p.Id,
            RegistrationId = p.RegistrationId,
            RegistrationNumber = p.Registration.RegistrationNumber,
            EventId = p.Registration.EventId,
            EventTitle = p.Registration.Event.Title,
            EventDepartmentCode = p.Registration.Event.Department?.Code,
            UserId = p.Registration.UserId,
            StudentName = p.Registration.User?.FullName ?? string.Empty,
            StudentEmail = p.Registration.User?.Email ?? string.Empty,
            StudentRollNumber = p.Registration.User?.RollNumber,
            StudentDepartment = p.Registration.User?.Department,
            StudentCollegeName = p.Registration.User?.CollegeName,
            OrderId = p.OrderId,
            TransactionId = p.TransactionId,
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status,
            PaymentMethod = p.PaymentMethod,
            InitiatedAt = p.InitiatedAt,
            CompletedAt = p.CompletedAt,
            FailureReason = p.FailureReason
        };
    }
}

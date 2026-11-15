using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing Phase 10: Attendance Management.
/// Enforces server-side ticket verification, cryptographic signatures, coordinator scoping,
/// anti-fraud duplicate prevention, atomic persistence, and manual check-in auditing.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeGenerator _qrGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        ApplicationDbContext context,
        IQrCodeGenerator qrGenerator,
        IAuditLogService auditLogService,
        ILogger<AttendanceService> logger)
    {
        _context = context;
        _qrGenerator = qrGenerator;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CheckInResultDto> CheckInAsync(
        CheckInRequestDto request,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Parameter Sanity Validation
        if (request == null || string.IsNullOrWhiteSpace(staffUserId) || string.IsNullOrWhiteSpace(staffRole))
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "Check-in request and staff credentials are required."
            };
        }

        if (request.EventId <= 0)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                Message = "A valid EventId must be provided."
            };
        }

        // 2. Staff Authorization & Event Scoping
        var canManage = await CanStaffManageEventAttendanceAsync(request.EventId, staffUserId, staffRole, cancellationToken);
        if (!canManage)
        {
            _logger.LogWarning("Unauthorized check-in attempt by staff {UserId} ({Role}) for Event {EventId}",
                staffUserId, staffRole, request.EventId);

            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.UnauthorizedStaff,
                EventId = request.EventId,
                Message = "You are not authorized to take attendance for this event."
            };
        }

        // 3. Event Verification
        var ev = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (ev == null)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.EventNotActive,
                EventId = request.EventId,
                Message = "Event not found or has been deleted."
            };
        }

        if (ev.Status == EventStatus.Cancelled)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.EventNotActive,
                EventId = request.EventId,
                EventTitle = ev.Title,
                Message = "Cannot record attendance for a cancelled event."
            };
        }

        // 4. Resolve Registration & Ticket based on input method
        string? targetTicketCode = request.TicketCode?.Trim();
        string? targetRegNumber = request.RegistrationNumber?.Trim();
        string? targetRollNumber = request.RollNumber?.Trim();

        // 4A. QR Payload Decryption & Signature Verification
        if (!string.IsNullOrWhiteSpace(request.QrPayload))
        {
            var (isSignatureValid, payloadTicketCode, payloadRegNumber, payloadEventId) =
                _qrGenerator.ParseAndVerifyPayload(request.QrPayload);

            if (!isSignatureValid)
            {
                _logger.LogWarning("QR check-in rejected: Cryptographic signature mismatch. Event: {EventId}", request.EventId);
                return new CheckInResultDto
                {
                    Success = false,
                    StatusCode = CheckInStatusCode.InvalidSignature,
                    EventId = request.EventId,
                    EventTitle = ev.Title,
                    Message = "Cryptographic signature mismatch. QR payload is invalid or tampered."
                };
            }

            // Event Reference Match from Signed Payload
            if (payloadEventId != request.EventId)
            {
                _logger.LogWarning("QR check-in rejected: Token Event #{TokenEventId} does not match active Event #{TargetEventId}",
                    payloadEventId, request.EventId);

                return new CheckInResultDto
                {
                    Success = false,
                    StatusCode = CheckInStatusCode.WrongEvent,
                    EventId = request.EventId,
                    EventTitle = ev.Title,
                    Message = $"This ticket belongs to Event #{payloadEventId}, not '{ev.Title}' (Event #{request.EventId})."
                };
            }

            targetTicketCode = payloadTicketCode;
            targetRegNumber = payloadRegNumber;
        }

        // 4B. Manual Check-In Remarks Enforcement
        if (request.CheckInMethod == CheckInMethod.Manual && string.IsNullOrWhiteSpace(request.Remarks))
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.MissingRequiredData,
                EventId = request.EventId,
                EventTitle = ev.Title,
                Message = "Remarks or justification is mandatory for manual roll-number check-in."
            };
        }

        // 5. Query Registration, Ticket, Payments and Existing Attendance
        EventRegistration? reg = null;

        if (!string.IsNullOrWhiteSpace(targetTicketCode))
        {
            var ticket = await _context.Tickets
                .Include(t => t.Registration)
                    .ThenInclude(r => r.User)
                .Include(t => t.Registration)
                    .ThenInclude(r => r.Event)
                .Include(t => t.Registration)
                    .ThenInclude(r => r.Payments)
                .Include(t => t.Registration)
                    .ThenInclude(r => r.Attendance)
                        .ThenInclude(a => a!.CheckedInByUser)
                .FirstOrDefaultAsync(t => t.TicketCode == targetTicketCode, cancellationToken);

            if (ticket != null)
            {
                reg = ticket.Registration;
                // Wire navigation
                reg.Ticket = ticket;
            }
        }
        else if (!string.IsNullOrWhiteSpace(targetRegNumber))
        {
            reg = await _context.EventRegistrations
                .Include(r => r.User)
                .Include(r => r.Event)
                .Include(r => r.Payments)
                .Include(r => r.Ticket)
                .Include(r => r.Attendance)
                    .ThenInclude(a => a!.CheckedInByUser)
                .FirstOrDefaultAsync(r => r.RegistrationNumber == targetRegNumber && r.EventId == request.EventId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(targetRollNumber))
        {
            reg = await _context.EventRegistrations
                .Include(r => r.User)
                .Include(r => r.Event)
                .Include(r => r.Payments)
                .Include(r => r.Ticket)
                .Include(r => r.Attendance)
                    .ThenInclude(a => a!.CheckedInByUser)
                .FirstOrDefaultAsync(r => r.User.RollNumber == targetRollNumber && r.EventId == request.EventId, cancellationToken);
        }

        if (reg == null)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.TicketNotFound,
                EventId = request.EventId,
                EventTitle = ev.Title,
                TicketCode = targetTicketCode,
                RegistrationNumber = targetRegNumber,
                StudentRollNumber = targetRollNumber,
                Message = "No matching registration or ticket found for this event."
            };
        }

        // 6. Cross-Match Event Reference
        if (reg.EventId != request.EventId)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.WrongEvent,
                EventId = request.EventId,
                EventTitle = ev.Title,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                StudentName = reg.User.FullName,
                Message = $"Registration belongs to Event #{reg.EventId} ('{reg.Event.Title}'), not Event #{request.EventId}."
            };
        }

        // 7. Ticket & Registration Lifecycle Rules
        if (reg.Ticket != null && !reg.Ticket.IsActive)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.InactiveTicket,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                TicketCode = reg.Ticket.TicketCode,
                StudentName = reg.User.FullName,
                Message = "This ticket has been deactivated or invalidated."
            };
        }

        if (reg.Status == RegistrationStatus.Cancelled)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.TicketCancelled,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                TicketCode = reg.Ticket?.TicketCode,
                StudentName = reg.User.FullName,
                Message = "Registration has been cancelled. Admission is denied."
            };
        }

        if (reg.Status != RegistrationStatus.Confirmed)
        {
            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.RegistrationNotConfirmed,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                StudentName = reg.User.FullName,
                Message = $"Registration is not confirmed (Current status: {reg.Status})."
            };
        }

        // 8. Payment Completeness & Refund Integrity for Paid Events
        if (reg.Event.EventType == EventType.Paid && reg.Event.RegistrationFee > 0)
        {
            var isRefunded = reg.Payments.Any(p => p.Status == PaymentStatus.Refunded);
            if (isRefunded)
            {
                _logger.LogWarning("Check-in rejected: Payment for Registration #{RegId} was refunded", reg.Id);
                return new CheckInResultDto
                {
                    Success = false,
                    StatusCode = CheckInStatusCode.PaymentRefunded,
                    EventId = reg.EventId,
                    EventTitle = reg.Event.Title,
                    RegistrationId = reg.Id,
                    RegistrationNumber = reg.RegistrationNumber,
                    StudentName = reg.User.FullName,
                    Message = "Payment for this registration has been refunded or reversed. Admission denied."
                };
            }

            var hasSuccessfulPayment = reg.Payments.Any(p => p.Status == PaymentStatus.Success);
            if (!hasSuccessfulPayment)
            {
                _logger.LogWarning("Check-in rejected: No verified payment for paid event. Reg #{RegId}", reg.Id);
                return new CheckInResultDto
                {
                    Success = false,
                    StatusCode = CheckInStatusCode.PaymentRequired,
                    EventId = reg.EventId,
                    EventTitle = reg.Event.Title,
                    RegistrationId = reg.Id,
                    RegistrationNumber = reg.RegistrationNumber,
                    StudentName = reg.User.FullName,
                    Message = "Paid event registration does not have verified payment. Admission denied."
                };
            }
        }

        // 9. Strict Duplicate Check-In Protection
        var existingAttendance = reg.Attendance ?? await _context.Attendances
            .Include(a => a.CheckedInByUser)
            .FirstOrDefaultAsync(a => a.RegistrationId == reg.Id, cancellationToken);

        if (existingAttendance != null || (reg.Ticket != null && reg.Ticket.IsUsed))
        {
            var checkedInTime = existingAttendance?.CheckedInAt ?? reg.Ticket?.UsedAt ?? DateTime.UtcNow;
            var verifierName = existingAttendance?.CheckedInByUser?.FullName ?? "Event Staff";

            _logger.LogWarning("Duplicate check-in blocked: Registration #{RegId} was already checked in at {Time} by {Staff}",
                reg.Id, checkedInTime, verifierName);

            var checkedInCountExisting = await _context.Attendances
                .CountAsync(a => a.Registration.EventId == request.EventId && a.Status == AttendanceStatus.CheckedIn, cancellationToken);

            var registeredCountExisting = await _context.EventRegistrations
                .CountAsync(r => r.EventId == request.EventId && r.Status == RegistrationStatus.Confirmed, cancellationToken);

            return new CheckInResultDto
            {
                Success = false,
                StatusCode = CheckInStatusCode.DuplicateCheckIn,
                AttendanceId = existingAttendance?.Id,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                TicketCode = reg.Ticket?.TicketCode,
                StudentUserId = reg.UserId,
                StudentName = reg.User.FullName,
                StudentEmail = reg.User.Email,
                StudentRollNumber = reg.User.RollNumber,
                StudentDepartment = reg.User.Department,
                CheckedInAt = checkedInTime,
                CheckedInByName = verifierName,
                CheckInMethod = existingAttendance?.CheckInMethod ?? CheckInMethod.QrCode,
                Remarks = existingAttendance?.Remarks,
                TotalCheckedIn = checkedInCountExisting,
                TotalRegistered = registeredCountExisting,
                Message = $"ALREADY CHECKED IN: Student was admitted on {checkedInTime:MMM dd, yyyy HH:mm} UTC by {verifierName}."
            };
        }

        // 10. Atomic Persistence under Execution Strategy
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Re-verify under transaction lock to eliminate race conditions
                var duplicateCheck = await _context.Attendances
                    .Include(a => a.CheckedInByUser)
                    .FirstOrDefaultAsync(a => a.RegistrationId == reg.Id, cancellationToken);

                if (duplicateCheck != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new CheckInResultDto
                    {
                        Success = false,
                        StatusCode = CheckInStatusCode.DuplicateCheckIn,
                        RegistrationId = reg.Id,
                        StudentName = reg.User.FullName,
                        CheckedInAt = duplicateCheck.CheckedInAt,
                        CheckedInByName = duplicateCheck.CheckedInByUser?.FullName ?? "Event Staff",
                        Message = $"Duplicate check-in prevented: Student was already checked in at {duplicateCheck.CheckedInAt:MMM dd, yyyy HH:mm} UTC."
                    };
                }

                var checkInTimestamp = DateTime.UtcNow;

                // 10A. Update Ticket if present
                if (reg.Ticket != null)
                {
                    reg.Ticket.IsUsed = true;
                    reg.Ticket.UsedAt = checkInTimestamp;
                    reg.Ticket.UpdatedAt = checkInTimestamp;
                }

                // 10B. Create Attendance Record
                var attendance = new Attendance
                {
                    RegistrationId = reg.Id,
                    CheckedInAt = checkInTimestamp,
                    CheckedInByUserId = staffUserId,
                    Status = AttendanceStatus.CheckedIn,
                    CheckInMethod = request.CheckInMethod,
                    Remarks = request.Remarks?.Trim(),
                    CreatedAt = checkInTimestamp,
                    IsActive = true
                };

                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync(cancellationToken);

                // 10C. Audit Log
                await _auditLogService.LogAsync(
                    action: request.CheckInMethod == CheckInMethod.Manual ? "ManualAttendanceCheckIn" : "AttendanceCheckIn",
                    entityName: "Attendance",
                    entityId: attendance.Id.ToString(),
                    userId: staffUserId,
                    newValues: $"RegId: {reg.Id}, EventId: {reg.EventId}, TicketCode: {reg.Ticket?.TicketCode}, Method: {request.CheckInMethod}, Remarks: {request.Remarks}"
                );

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Successfully checked in Registration #{RegNum} (Ticket {TicketCode}) for Event #{EventId} via {Method}",
                    reg.RegistrationNumber, reg.Ticket?.TicketCode ?? "N/A", reg.EventId, request.CheckInMethod);

                // 10D. Calculate updated metrics
                var totalCheckedIn = await _context.Attendances
                    .CountAsync(a => a.Registration.EventId == request.EventId && a.Status == AttendanceStatus.CheckedIn, cancellationToken);

                var totalRegistered = await _context.EventRegistrations
                    .CountAsync(r => r.EventId == request.EventId && r.Status == RegistrationStatus.Confirmed, cancellationToken);

                var staffUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == staffUserId, cancellationToken);

                return new CheckInResultDto
                {
                    Success = true,
                    StatusCode = CheckInStatusCode.Success,
                    AttendanceId = attendance.Id,
                    RegistrationId = reg.Id,
                    RegistrationNumber = reg.RegistrationNumber,
                    TicketCode = reg.Ticket?.TicketCode,
                    EventId = reg.EventId,
                    EventTitle = reg.Event.Title,
                    StudentUserId = reg.UserId,
                    StudentName = reg.User.FullName,
                    StudentEmail = reg.User.Email,
                    StudentRollNumber = reg.User.RollNumber,
                    StudentDepartment = reg.User.Department,
                    CheckedInAt = checkInTimestamp,
                    CheckedInByUserId = staffUserId,
                    CheckedInByName = staffUser?.FullName ?? "Event Staff",
                    CheckInMethod = request.CheckInMethod,
                    Remarks = request.Remarks,
                    TotalCheckedIn = totalCheckedIn,
                    TotalRegistered = totalRegistered,
                    Message = "ADMISSION CONFIRMED: Student successfully checked in."
                };
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Concurrency collision during check-in for Registration #{RegId}. Checking for duplicate.", reg.Id);

                var existing = await _context.Attendances
                    .Include(a => a.CheckedInByUser)
                    .FirstOrDefaultAsync(a => a.RegistrationId == reg.Id, cancellationToken);

                if (existing != null)
                {
                    return new CheckInResultDto
                    {
                        Success = false,
                        StatusCode = CheckInStatusCode.DuplicateCheckIn,
                        RegistrationId = reg.Id,
                        StudentName = reg.User.FullName,
                        CheckedInAt = existing.CheckedInAt,
                        CheckedInByName = existing.CheckedInByUser?.FullName ?? "Event Staff",
                        Message = $"Duplicate check-in prevented: Student was already checked in at {existing.CheckedInAt:MMM dd, yyyy HH:mm} UTC."
                    };
                }

                return new CheckInResultDto
                {
                    Success = false,
                    StatusCode = CheckInStatusCode.ConcurrencyError,
                    EventId = request.EventId,
                    Message = "Database concurrency conflict occurred during check-in. Please try again."
                };
            }
        });
    }

    /// <inheritdoc />
    public async Task<EventAttendanceSummaryDto> GetEventAttendanceSummaryAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        var canManage = await CanStaffManageEventAttendanceAsync(eventId, staffUserId, staffRole, cancellationToken);
        if (!canManage)
        {
            throw new UnauthorizedAccessException($"Staff {staffUserId} is not authorized to access attendance summary for Event #{eventId}.");
        }

        var ev = await _context.Events
            .AsNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev == null)
        {
            throw new InvalidOperationException($"Event #{eventId} was not found.");
        }

        var totalRegistered = await _context.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed, cancellationToken);

        var totalCheckedIn = await _context.Attendances
            .CountAsync(a => a.Registration.EventId == eventId && a.Status == AttendanceStatus.CheckedIn, cancellationToken);

        return new EventAttendanceSummaryDto
        {
            EventId = ev.Id,
            EventTitle = ev.Title,
            DepartmentName = ev.Department?.Name ?? "General",
            EventStartDate = ev.EventStartDate,
            EventEndDate = ev.EventEndDate,
            Venue = ev.Venue,
            TotalCapacity = ev.TotalCapacity,
            TotalRegistered = totalRegistered,
            TotalCheckedIn = totalCheckedIn
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<AttendanceRecordDto>> GetAttendanceRecordsAsync(
        AttendanceFilterCriteria criteria,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new AttendanceFilterCriteria();

        // If Coordinator, event scoping is mandatory
        if (staffRole == nameof(UserRole.Coordinator))
        {
            if (!criteria.EventId.HasValue || criteria.EventId.Value <= 0)
            {
                throw new ArgumentException("EventId is required for Coordinator attendance queries.", nameof(criteria));
            }

            var canManage = await CanStaffManageEventAttendanceAsync(criteria.EventId.Value, staffUserId, staffRole, cancellationToken);
            if (!canManage)
            {
                throw new UnauthorizedAccessException($"Coordinator is not authorized for Event #{criteria.EventId.Value}.");
            }
        }

        var query = _context.Attendances
            .AsNoTracking()
            .Include(a => a.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(a => a.Registration)
                .ThenInclude(r => r.User)
            .Include(a => a.Registration)
                .ThenInclude(r => r.Ticket)
            .Include(a => a.CheckedInByUser)
            .Where(a => !a.Registration.Event.IsDeleted);

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(a => a.Registration.EventId == criteria.EventId.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(a => a.Registration.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.CheckInMethod.HasValue)
        {
            query = query.Where(a => a.CheckInMethod == criteria.CheckInMethod.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Registration.RegistrationNumber.ToLower().Contains(term) ||
                a.Registration.User.FirstName.ToLower().Contains(term) ||
                a.Registration.User.LastName.ToLower().Contains(term) ||
                (a.Registration.User.RollNumber != null && a.Registration.User.RollNumber.ToLower().Contains(term)) ||
                (a.Registration.Ticket != null && a.Registration.Ticket.TicketCode.ToLower().Contains(term)) ||
                a.Registration.Event.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CheckedInAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceRecordDto
            {
                Id = a.Id,
                RegistrationId = a.RegistrationId,
                RegistrationNumber = a.Registration.RegistrationNumber,
                EventId = a.Registration.EventId,
                EventTitle = a.Registration.Event.Title,
                StudentUserId = a.Registration.UserId,
                StudentName = a.Registration.User.FullName,
                StudentEmail = a.Registration.User.Email ?? string.Empty,
                StudentRollNumber = a.Registration.User.RollNumber,
                StudentDepartment = a.Registration.User.Department,
                TicketCode = a.Registration.Ticket != null ? a.Registration.Ticket.TicketCode : null,
                CheckedInAt = a.CheckedInAt,
                CheckedInByUserId = a.CheckedInByUserId,
                CheckedInByName = a.CheckedInByUser.FullName,
                Status = a.Status,
                CheckInMethod = a.CheckInMethod,
                Remarks = a.Remarks
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AttendanceRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<AttendanceRecordDto?> GetAttendanceDetailsAsync(
        int attendanceId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        var attendance = await _context.Attendances
            .AsNoTracking()
            .Include(a => a.Registration)
                .ThenInclude(r => r.Event)
            .Include(a => a.Registration)
                .ThenInclude(r => r.User)
            .Include(a => a.Registration)
                .ThenInclude(r => r.Ticket)
            .Include(a => a.CheckedInByUser)
            .FirstOrDefaultAsync(a => a.Id == attendanceId && !a.Registration.Event.IsDeleted, cancellationToken);

        if (attendance == null)
        {
            return null;
        }

        // Coordinator Event-Scoping
        if (staffRole == nameof(UserRole.Coordinator))
        {
            var canManage = await CanStaffManageEventAttendanceAsync(attendance.Registration.EventId, staffUserId, staffRole, cancellationToken);
            if (!canManage)
            {
                _logger.LogWarning("Coordinator {UserId} denied access to attendance #{AttendanceId} for unassigned Event #{EventId}",
                    staffUserId, attendanceId, attendance.Registration.EventId);
                return null;
            }
        }

        return new AttendanceRecordDto
        {
            Id = attendance.Id,
            RegistrationId = attendance.RegistrationId,
            RegistrationNumber = attendance.Registration.RegistrationNumber,
            EventId = attendance.Registration.EventId,
            EventTitle = attendance.Registration.Event.Title,
            StudentUserId = attendance.Registration.UserId,
            StudentName = attendance.Registration.User.FullName,
            StudentEmail = attendance.Registration.User.Email ?? string.Empty,
            StudentRollNumber = attendance.Registration.User.RollNumber,
            StudentDepartment = attendance.Registration.User.Department,
            TicketCode = attendance.Registration.Ticket?.TicketCode,
            CheckedInAt = attendance.CheckedInAt,
            CheckedInByUserId = attendance.CheckedInByUserId,
            CheckedInByName = attendance.CheckedInByUser?.FullName ?? "Event Staff",
            Status = attendance.Status,
            CheckInMethod = attendance.CheckInMethod,
            Remarks = attendance.Remarks
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsStudentCheckedInAsync(
        int registrationId,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0) return false;

        return await _context.Attendances
            .AnyAsync(a => a.RegistrationId == registrationId && a.Status == AttendanceStatus.CheckedIn, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CanStaffManageEventAttendanceAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0 || string.IsNullOrWhiteSpace(staffUserId))
        {
            return false;
        }

        // Admins and SuperAdmins have cross-campus management authority
        if (staffRole == nameof(UserRole.Admin) || staffRole == nameof(UserRole.SuperAdmin))
        {
            return true;
        }

        // Coordinators must be assigned to the event (CreatedBy or CoordinatedEvents)
        return await _context.Events
            .AnyAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.CreatedByUserId == staffUserId || e.Coordinators.Any(c => c.UserId == staffUserId)),
                cancellationToken);
    }
}

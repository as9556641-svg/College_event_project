using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing Phase 9 QR Ticket Generation & Validation.
/// Enforces server-side eligibility rules, cryptographic verification,
/// duplicate generation prevention, lifecycle invalidation, and role-based scoping.
/// </summary>
public class TicketService : ITicketService
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeGenerator _qrGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ApplicationDbContext context,
        IQrCodeGenerator qrGenerator,
        IAuditLogService auditLogService,
        ILogger<TicketService> logger)
    {
        _context = context;
        _qrGenerator = qrGenerator;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TicketDto?> GetOrGenerateTicketForStudentAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (registrationId <= 0 || string.IsNullOrWhiteSpace(studentUserId))
        {
            return null;
        }

        var reg = await _context.EventRegistrations
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.User)
            .Include(r => r.Payments)
            .Include(r => r.Ticket)
            .FirstOrDefaultAsync(r => r.Id == registrationId && !r.Event.IsDeleted, cancellationToken);

        if (reg == null)
        {
            return null;
        }

        // STRICT Student Tenant Isolation
        if (reg.UserId != studentUserId)
        {
            _logger.LogWarning("Security breach attempt: Student {UserId} attempted to access ticket for registration #{RegId} owned by {OwnerId}",
                studentUserId, registrationId, reg.UserId);
            return null;
        }

        // Check if ticket already exists
        if (reg.Ticket != null)
        {
            return MapToDto(reg.Ticket, reg);
        }

        // Check eligibility for on-demand generation
        var isEligible = IsRegistrationEligibleForTicket(reg);
        if (!isEligible)
        {
            return null;
        }

        // Generate and persist
        return await GenerateTicketInternalAsync(reg, studentUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TicketDto?> GetTicketByCodeForStudentAsync(
        string ticketCode,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketCode) || string.IsNullOrWhiteSpace(studentUserId))
        {
            return null;
        }

        var ticket = await _context.Tickets
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .FirstOrDefaultAsync(t => t.TicketCode == ticketCode.Trim(), cancellationToken);

        if (ticket == null)
        {
            return null;
        }

        // STRICT Student Tenant Isolation
        if (ticket.Registration.UserId != studentUserId)
        {
            _logger.LogWarning("Tenant violation attempt: User {UserId} tried accessing ticket {Code} owned by {OwnerId}",
                studentUserId, ticketCode, ticket.Registration.UserId);
            return null;
        }

        return MapToDto(ticket, ticket.Registration);
    }

    /// <inheritdoc />
    public async Task<PagedResult<TicketDto>> GetStudentTicketsAsync(
        string studentUserId,
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new TicketFilterCriteria();

        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .Where(t => t.Registration.UserId == studentUserId && !t.Registration.Event.IsDeleted);

        if (criteria.IsUsed.HasValue)
        {
            query = query.Where(t => t.IsUsed == criteria.IsUsed.Value);
        }

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == criteria.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.TicketCode.ToLower().Contains(term) ||
                t.Registration.RegistrationNumber.ToLower().Contains(term) ||
                t.Registration.Event.Title.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 50);

        var tickets = await query
            .OrderByDescending(t => t.IssuedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = tickets.Select(t => MapToDto(t, t.Registration)).ToList();

        return new PagedResult<TicketDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<TicketDto> GenerateTicketAsync(
        int registrationId,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        var reg = await _context.EventRegistrations
            .Include(r => r.Event)
                .ThenInclude(e => e.Department)
            .Include(r => r.User)
            .Include(r => r.Payments)
            .Include(r => r.Ticket)
            .FirstOrDefaultAsync(r => r.Id == registrationId && !r.Event.IsDeleted, cancellationToken);

        if (reg == null)
        {
            throw new InvalidOperationException($"Registration #{registrationId} was not found.");
        }

        if (reg.Ticket != null)
        {
            return MapToDto(reg.Ticket, reg);
        }

        // Strict Server-Side Eligibility Validation
        if (reg.Status != RegistrationStatus.Confirmed)
        {
            throw new InvalidOperationException($"Cannot generate ticket for registration with status '{reg.Status}'. Ticket generation requires Confirmed status.");
        }

        if (reg.Event.EventType == EventType.Paid && reg.Event.RegistrationFee > 0)
        {
            var hasPaid = reg.Payments.Any(p => p.Status == PaymentStatus.Success);
            if (!hasPaid)
            {
                throw new InvalidOperationException("Cannot generate ticket for paid event without verified successful payment.");
            }
        }

        return await GenerateTicketInternalAsync(reg, studentUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TicketValidationResultDto> ValidateTicketPayloadAsync(
        string qrPayload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrPayload))
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                Message = "QR payload is empty."
            };
        }

        // 1. Independent cryptographic signature validation
        var (isSignatureValid, ticketCode, regNumber, eventId) = _qrGenerator.ParseAndVerifyPayload(qrPayload);
        if (!isSignatureValid)
        {
            _logger.LogWarning("Cryptographic QR signature mismatch or tampered payload: {Payload}", qrPayload);
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                Message = "Cryptographic signature mismatch. QR payload is forged or tampered."
            };
        }

        // 2. Database validation of ticket entity and parent lifecycle
        return await ValidateTicketCodeInternalAsync(ticketCode, regNumber, eventId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TicketValidationResultDto> ValidateTicketCodeAsync(
        string ticketCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketCode))
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                Message = "Ticket code is required."
            };
        }

        return await ValidateTicketCodeInternalAsync(ticketCode.Trim(), null, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> InvalidateTicketAsync(
        int registrationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.RegistrationId == registrationId, cancellationToken);

        if (ticket == null || !ticket.IsActive)
        {
            return false;
        }

        ticket.IsActive = false;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            action: "InvalidateTicket",
            entityName: "Ticket",
            entityId: ticket.Id.ToString(),
            userId: null,
            newValues: $"TicketCode: {ticket.TicketCode}, Reason: {reason}"
        );

        _logger.LogInformation("Ticket {Code} for Registration #{RegId} invalidated. Reason: {Reason}",
            ticket.TicketCode, registrationId, reason);

        return true;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TicketDto>> GetTicketsForAdminAsync(
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new TicketFilterCriteria();

        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .Where(t => !t.Registration.Event.IsDeleted);

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            query = query.Where(t => t.Registration.EventId == criteria.EventId.Value);
        }

        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(t => t.Registration.Event.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.IsUsed.HasValue)
        {
            query = query.Where(t => t.IsUsed == criteria.IsUsed.Value);
        }

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == criteria.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.TicketCode.ToLower().Contains(term) ||
                t.Registration.RegistrationNumber.ToLower().Contains(term) ||
                t.Registration.Event.Title.ToLower().Contains(term) ||
                t.Registration.User.FirstName.ToLower().Contains(term) ||
                t.Registration.User.LastName.ToLower().Contains(term) ||
                (t.Registration.User.RollNumber != null && t.Registration.User.RollNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var tickets = await query
            .OrderByDescending(t => t.IssuedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = tickets.Select(t => MapToDto(t, t.Registration)).ToList();

        return new PagedResult<TicketDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<TicketDto>> GetTicketsForCoordinatorAsync(
        int eventId,
        string coordinatorUserId,
        TicketFilterCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (eventId <= 0 || string.IsNullOrWhiteSpace(coordinatorUserId))
        {
            return new PagedResult<TicketDto> { Items = new List<TicketDto>() };
        }

        // STRICT Coordinator Scope Validation
        var isAssigned = await _context.Events
            .AnyAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.CreatedByUserId == coordinatorUserId || e.Coordinators.Any(c => c.UserId == coordinatorUserId)), cancellationToken);

        if (!isAssigned)
        {
            _logger.LogWarning("Coordinator {UserId} unauthorized access attempt to tickets for Event {EventId}",
                coordinatorUserId, eventId);
            throw new UnauthorizedAccessException($"Coordinator is not authorized to view tickets for Event #{eventId}.");
        }

        criteria ??= new TicketFilterCriteria();
        criteria.EventId = eventId;

        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .Where(t => t.Registration.EventId == eventId && !t.Registration.Event.IsDeleted);

        if (criteria.IsUsed.HasValue)
        {
            query = query.Where(t => t.IsUsed == criteria.IsUsed.Value);
        }

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == criteria.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.TicketCode.ToLower().Contains(term) ||
                t.Registration.RegistrationNumber.ToLower().Contains(term) ||
                t.Registration.User.FirstName.ToLower().Contains(term) ||
                t.Registration.User.LastName.ToLower().Contains(term) ||
                (t.Registration.User.RollNumber != null && t.Registration.User.RollNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        var tickets = await query
            .OrderByDescending(t => t.IssuedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = tickets.Select(t => MapToDto(t, t.Registration)).ToList();

        return new PagedResult<TicketDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<TicketDto?> GetTicketDetailsForStaffAsync(
        int ticketId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
                    .ThenInclude(e => e.Department)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .FirstOrDefaultAsync(t => t.Id == ticketId && !t.Registration.Event.IsDeleted, cancellationToken);

        if (ticket == null)
        {
            return null;
        }

        // Coordinator Scope Verification
        if (staffRole == nameof(UserRole.Coordinator))
        {
            var isAssigned = await _context.Events
                .AnyAsync(e => e.Id == ticket.Registration.EventId && !e.IsDeleted &&
                    (e.CreatedByUserId == staffUserId || e.Coordinators.Any(c => c.UserId == staffUserId)), cancellationToken);

            if (!isAssigned)
            {
                _logger.LogWarning("Coordinator {UserId} denied access to ticket #{TicketId} for unassigned Event {EventId}",
                    staffUserId, ticketId, ticket.Registration.EventId);
                return null;
            }
        }

        return MapToDto(ticket, ticket.Registration);
    }

    private async Task<TicketDto> GenerateTicketInternalAsync(
        EventRegistration reg,
        string studentUserId,
        CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Re-check for duplicate under transaction
            var existing = await _context.Tickets
                .FirstOrDefaultAsync(t => t.RegistrationId == reg.Id, cancellationToken);

            if (existing != null)
            {
                return MapToDto(existing, reg);
            }

            var ticketCode = $"TKT-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            var qrPayload = _qrGenerator.GenerateSignedPayload(ticketCode, reg.RegistrationNumber, reg.EventId);

            var ticket = new Ticket
            {
                RegistrationId = reg.Id,
                TicketCode = ticketCode,
                QrCodeData = qrPayload,
                IssuedAt = DateTime.UtcNow,
                IsActive = true,
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync(cancellationToken);

                await _auditLogService.LogAsync(
                    action: "GenerateTicket",
                    entityName: "Ticket",
                    entityId: ticket.Id.ToString(),
                    userId: studentUserId,
                    newValues: $"TicketCode: {ticket.TicketCode}, RegId: {reg.Id}, EventId: {reg.EventId}"
                );

                _logger.LogInformation("Generated Ticket {TicketCode} for Registration #{RegNum} (Event #{EventId})",
                    ticket.TicketCode, reg.RegistrationNumber, reg.EventId);

                return MapToDto(ticket, reg);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Concurrency collision generating ticket for Registration #{RegId}. Checking for existing ticket.", reg.Id);

                var duplicate = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.RegistrationId == reg.Id, cancellationToken);

                if (duplicate != null)
                {
                    return MapToDto(duplicate, reg);
                }

                throw;
            }
        });
    }

    private async Task<TicketValidationResultDto> ValidateTicketCodeInternalAsync(
        string ticketCode,
        string? expectedRegNumber,
        int? expectedEventId,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.Tickets
            .Include(t => t.Registration)
                .ThenInclude(r => r.Event)
            .Include(t => t.Registration)
                .ThenInclude(r => r.User)
            .Include(t => t.Registration)
                .ThenInclude(r => r.Payments)
            .FirstOrDefaultAsync(t => t.TicketCode == ticketCode, cancellationToken);

        if (ticket == null)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                TicketCode = ticketCode,
                Message = $"Ticket with code '{ticketCode}' was not found in the system."
            };
        }

        var reg = ticket.Registration;

        // Cross-check registration and event match if provided in signed token
        if (!string.IsNullOrEmpty(expectedRegNumber) && reg.RegistrationNumber != expectedRegNumber)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                TicketCode = ticketCode,
                Message = "Registration number on token does not match database record."
            };
        }

        if (expectedEventId.HasValue && expectedEventId.Value > 0 && reg.EventId != expectedEventId.Value)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                TicketCode = ticketCode,
                Message = "Event ID on token does not match database record."
            };
        }

        // Check if event was deleted
        if (reg.Event.IsDeleted)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                TicketCode = ticketCode,
                Message = "The event associated with this ticket has been deleted."
            };
        }

        // Check if ticket is inactive or cancelled
        if (!ticket.IsActive || reg.Status == RegistrationStatus.Cancelled)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Cancelled,
                TicketCode = ticketCode,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                StudentName = reg.User?.FullName,
                Message = "Ticket is cancelled or invalidated."
            };
        }

        // Check registration status is Confirmed
        if (reg.Status != RegistrationStatus.Confirmed)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Invalid,
                TicketCode = ticketCode,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                StudentName = reg.User?.FullName,
                Message = $"Registration is not confirmed (Current status: {reg.Status})."
            };
        }

        // Check paid event has verified successful payment
        if (reg.Event.EventType == EventType.Paid && reg.Event.RegistrationFee > 0)
        {
            var hasPaid = reg.Payments.Any(p => p.Status == PaymentStatus.Success);
            if (!hasPaid)
            {
                return new TicketValidationResultDto
                {
                    IsValid = false,
                    Status = TicketStatus.Invalid,
                    TicketCode = ticketCode,
                    RegistrationId = reg.Id,
                    RegistrationNumber = reg.RegistrationNumber,
                    EventId = reg.EventId,
                    EventTitle = reg.Event.Title,
                    StudentName = reg.User?.FullName,
                    Message = "Payment verification missing for this paid event ticket."
                };
            }
        }

        // Check if already used
        if (ticket.IsUsed)
        {
            return new TicketValidationResultDto
            {
                IsValid = false,
                Status = TicketStatus.Used,
                TicketCode = ticketCode,
                RegistrationId = reg.Id,
                RegistrationNumber = reg.RegistrationNumber,
                EventId = reg.EventId,
                EventTitle = reg.Event.Title,
                StudentName = reg.User?.FullName,
                Ticket = MapToDto(ticket, reg),
                Message = $"Ticket was already used for admission on {ticket.UsedAt:MMM dd, yyyy HH:mm} UTC."
            };
        }

        // All checks passed -> Valid!
        return new TicketValidationResultDto
        {
            IsValid = true,
            Status = TicketStatus.Valid,
            TicketCode = ticketCode,
            RegistrationId = reg.Id,
            RegistrationNumber = reg.RegistrationNumber,
            EventId = reg.EventId,
            EventTitle = reg.Event.Title,
            StudentName = reg.User?.FullName,
            Ticket = MapToDto(ticket, reg),
            Message = "Ticket is valid for event admission."
        };
    }

    private static bool IsRegistrationEligibleForTicket(EventRegistration reg)
    {
        if (reg.Status != RegistrationStatus.Confirmed)
        {
            return false;
        }

        if (reg.Event.EventType == EventType.Paid && reg.Event.RegistrationFee > 0)
        {
            return reg.Payments.Any(p => p.Status == PaymentStatus.Success);
        }

        return true;
    }

    private TicketDto MapToDto(Ticket t, EventRegistration reg)
    {
        var status = TicketStatus.Valid;
        var statusMsg = "Valid for Admission";

        if (!t.IsActive || reg.Status == RegistrationStatus.Cancelled)
        {
            status = TicketStatus.Cancelled;
            statusMsg = "Ticket Inactive / Cancelled";
        }
        else if (t.IsUsed)
        {
            status = TicketStatus.Used;
            statusMsg = $"Used on {t.UsedAt:MMM dd, yyyy HH:mm}";
        }
        else if (reg.Status != RegistrationStatus.Confirmed)
        {
            status = TicketStatus.Invalid;
            statusMsg = $"Registration {reg.Status}";
        }

        var qrBase64 = _qrGenerator.GenerateQrPngBase64(t.QrCodeData);

        return new TicketDto
        {
            Id = t.Id,
            RegistrationId = t.RegistrationId,
            RegistrationNumber = reg.RegistrationNumber,
            TicketCode = t.TicketCode,
            QrCodeData = t.QrCodeData,
            QrCodeBase64 = qrBase64,
            EventId = reg.EventId,
            EventTitle = reg.Event.Title,
            EventSlug = reg.Event.Slug,
            EventVenue = reg.Event.Venue,
            EventMode = reg.Event.EventMode,
            EventType = reg.Event.EventType,
            RegistrationFee = reg.Event.RegistrationFee,
            EventStartDate = reg.Event.EventStartDate,
            EventEndDate = reg.Event.EventEndDate,
            EventDepartmentCode = reg.Event.Department?.Code,
            UserId = reg.UserId,
            StudentName = reg.User?.FullName ?? string.Empty,
            StudentRollNumber = reg.User?.RollNumber,
            StudentDepartment = reg.User?.Department,
            StudentCollegeName = reg.User?.CollegeName,
            IssuedAt = t.IssuedAt,
            IsUsed = t.IsUsed,
            UsedAt = t.UsedAt,
            IsActive = t.IsActive,
            Status = status,
            StatusMessage = statusMsg
        };
    }
}

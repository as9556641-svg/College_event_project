using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRealTimeNotificationPublisher? _realTimePublisher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationService> logger,
        IRealTimeNotificationPublisher? realTimePublisher = null)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _realTimePublisher = realTimePublisher;
    }

    public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(
        string userId,
        int page = 1,
        int pageSize = 10,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new PagedResult<NotificationDto> { PageNumber = page, PageSize = pageSize };
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.IsActive);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                EventId = n.EventId,
                EventTitle = n.Event != null ? n.Event.Title : null,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                TargetRole = n.TargetRole,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                ActionUrl = n.ActionUrl,
                CorrelationId = n.CorrelationId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead && n.IsActive, cancellationToken);
    }

    public async Task<NotificationDto?> GetNotificationByIdAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Notifications
            .Include(n => n.Event)
            .FirstOrDefaultAsync(n => n.Id == id && n.IsActive, cancellationToken);

        if (entity == null) return null;

        // Strict tenant isolation: Student / User can only inspect their own notification
        if (entity.UserId != requestingUserId)
        {
            _logger.LogWarning(
                "Tenant violation attempt: User {RequestingUser} attempted to access notification #{NotificationId} belonging to user {OwnerUser}",
                requestingUserId, id, entity.UserId);

            throw new UnauthorizedAccessException("You are not authorized to view this notification.");
        }

        return new NotificationDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            EventId = entity.EventId,
            EventTitle = entity.Event?.Title,
            Title = entity.Title,
            Message = entity.Message,
            Type = entity.Type,
            TargetRole = entity.TargetRole,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            ActionUrl = entity.ActionUrl,
            CorrelationId = entity.CorrelationId,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<bool> MarkAsReadAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.IsActive, cancellationToken);

        if (entity == null) return false;

        // Ownership enforcement
        if (entity.UserId != requestingUserId)
        {
            _logger.LogWarning(
                "Security violation: User {RequestingUser} tried marking notification #{NotificationId} owned by {OwnerUser}",
                requestingUserId, id, entity.UserId);

            throw new UnauthorizedAccessException("You do not own this notification.");
        }

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            if (_realTimePublisher != null)
            {
                var unreadCount = await GetUnreadCountAsync(requestingUserId, cancellationToken);
                await _realTimePublisher.UpdateUnreadCountAsync(requestingUserId, unreadCount, cancellationToken);
            }
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;

        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && n.IsActive)
            .ToListAsync(cancellationToken);

        if (!unreadNotifications.Any()) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unreadNotifications)
        {
            n.IsRead = true;
            n.ReadAt = now;
            n.UpdatedAt = now;
        }

        var count = await _context.SaveChangesAsync(cancellationToken);

        if (_realTimePublisher != null)
        {
            await _realTimePublisher.UpdateUnreadCountAsync(userId, 0, cancellationToken);
        }

        return count;
    }

    public async Task<bool> DeleteNotificationAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.IsActive, cancellationToken);

        if (entity == null) return false;

        if (entity.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("You do not own this notification.");
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        if (_realTimePublisher != null)
        {
            var unreadCount = await GetUnreadCountAsync(requestingUserId, cancellationToken);
            await _realTimePublisher.UpdateUnreadCountAsync(requestingUserId, unreadCount, cancellationToken);
        }

        return true;
    }

    public async Task<NotificationDto?> CreateNotificationAsync(
        CreateNotificationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
        {
            return null;
        }

        // Deduplication Check via CorrelationId
        if (!string.IsNullOrWhiteSpace(dto.CorrelationId))
        {
            var existing = await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == dto.UserId && n.CorrelationId == dto.CorrelationId && n.IsActive, cancellationToken);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Duplicate notification avoided for user {UserId} with CorrelationId '{CorrelationId}' (Existing Notification #{Id})",
                    dto.UserId, dto.CorrelationId, existing.Id);

                return new NotificationDto
                {
                    Id = existing.Id,
                    UserId = existing.UserId,
                    EventId = existing.EventId,
                    Title = existing.Title,
                    Message = existing.Message,
                    Type = existing.Type,
                    TargetRole = existing.TargetRole,
                    IsRead = existing.IsRead,
                    ReadAt = existing.ReadAt,
                    ActionUrl = existing.ActionUrl,
                    CorrelationId = existing.CorrelationId,
                    CreatedAt = existing.CreatedAt
                };
            }
        }

        var notification = new Notification
        {
            UserId = dto.UserId,
            EventId = dto.EventId,
            Title = dto.Title.Trim(),
            Message = dto.Message.Trim(),
            Type = dto.Type,
            TargetRole = dto.TargetRole,
            ActionUrl = dto.ActionUrl,
            CorrelationId = dto.CorrelationId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        var resultDto = new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            EventId = notification.EventId,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            TargetRole = notification.TargetRole,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            ActionUrl = notification.ActionUrl,
            CorrelationId = notification.CorrelationId,
            CreatedAt = notification.CreatedAt
        };

        // Real-Time SignalR Dispatch
        if (_realTimePublisher != null)
        {
            var unreadCount = await GetUnreadCountAsync(dto.UserId, cancellationToken);
            await _realTimePublisher.PublishNotificationAsync(dto.UserId, resultDto, unreadCount, cancellationToken);
        }

        return resultDto;
    }

    public async Task<int> BroadcastRoleNotificationAsync(
        string role,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        int? eventId = null,
        string? correlationPrefix = null,
        CancellationToken cancellationToken = default)
    {
        var usersInRole = await _userManager.GetUsersInRoleAsync(role);
        if (usersInRole == null || !usersInRole.Any()) return 0;

        int count = 0;
        foreach (var user in usersInRole)
        {
            var corrId = !string.IsNullOrEmpty(correlationPrefix) ? $"{correlationPrefix}-{user.Id}" : null;
            var created = await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = type,
                TargetRole = role,
                ActionUrl = actionUrl,
                EventId = eventId,
                CorrelationId = corrId
            }, cancellationToken);

            if (created != null) count++;
        }

        return count;
    }

    public async Task<int> BroadcastEventNotificationAsync(
        int eventId,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        string? correlationPrefix = null,
        CancellationToken cancellationToken = default)
    {
        var attendees = await _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed && r.IsActive)
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!attendees.Any()) return 0;

        int count = 0;
        foreach (var userId in attendees)
        {
            var corrId = !string.IsNullOrEmpty(correlationPrefix) ? $"{correlationPrefix}-{userId}" : null;
            var created = await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId,
                EventId = eventId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                CorrelationId = corrId
            }, cancellationToken);

            if (created != null) count++;
        }

        return count;
    }
}

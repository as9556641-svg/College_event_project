using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for managing user notifications, unread counters, and event triggers.
/// </summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(
        string userId,
        int page = 1,
        int pageSize = 10,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<NotificationDto?> GetNotificationByIdAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteNotificationAsync(
        int id,
        string requestingUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification with automatic deduplication when CorrelationId is specified.
    /// </summary>
    Task<NotificationDto?> CreateNotificationAsync(
        CreateNotificationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts in-app notification to all users matching a specific role (e.g., Coordinator, Admin, Student).
    /// </summary>
    Task<int> BroadcastRoleNotificationAsync(
        string role,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        int? eventId = null,
        string? correlationPrefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts in-app notification to all confirmed attendees of a specific event.
    /// </summary>
    Task<int> BroadcastEventNotificationAsync(
        int eventId,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        string? correlationPrefix = null,
        CancellationToken cancellationToken = default);
}

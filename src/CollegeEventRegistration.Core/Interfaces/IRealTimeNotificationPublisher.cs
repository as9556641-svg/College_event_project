using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Abstraction for publishing notifications to real-time clients (SignalR).
/// </summary>
public interface IRealTimeNotificationPublisher
{
    Task PublishNotificationAsync(
        string userId,
        NotificationDto notification,
        int unreadCount,
        CancellationToken cancellationToken = default);

    Task UpdateUnreadCountAsync(
        string userId,
        int unreadCount,
        CancellationToken cancellationToken = default);
}

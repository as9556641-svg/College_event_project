using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CollegeEventRegistration.Web.Services;

public class SignalRNotificationPublisher : IRealTimeNotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationPublisher> _logger;

    public SignalRNotificationPublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishNotificationAsync(
        string userId,
        NotificationDto notification,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            // Strictly isolated to targeted user identifier
            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notification, cancellationToken);
            await _hubContext.Clients.User(userId).SendAsync("UpdateUnreadCount", unreadCount, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push real-time SignalR notification to user {UserId}", userId);
        }
    }

    public async Task UpdateUnreadCountAsync(
        string userId,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;

        try
        {
            await _hubContext.Clients.User(userId).SendAsync("UpdateUnreadCount", unreadCount, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update real-time unread count for user {UserId}", userId);
        }
    }
}

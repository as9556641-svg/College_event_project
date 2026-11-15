using System.Security.Claims;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CollegeEventRegistration.Web.Hubs;

/// <summary>
/// Authenticated SignalR Hub for real-time notification dispatch and badge count sync.
/// Strict user isolation: Context.UserIdentifier isolates notifications per user.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;

    public NotificationHub(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UpdateUnreadCount", unreadCount);
        }
        await base.OnConnectedAsync();
    }

    public async Task<int> GetMyUnreadCount()
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return 0;
        return await _notificationService.GetUnreadCountAsync(userId);
    }
}

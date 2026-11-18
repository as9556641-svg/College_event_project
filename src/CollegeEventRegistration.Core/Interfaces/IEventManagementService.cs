using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

public interface IEventManagementService
{
    Task<(bool Succeeded, string? ErrorMessage, Event? Event)> CreateEventAsync(CreateEventDto dto, string createdByUserId);
    Task<(bool Succeeded, string? ErrorMessage, Event? Event)> UpdateEventAsync(int eventId, UpdateEventDto dto, string currentUserId, bool isElevatedAdmin);
    Task<Event?> GetEventDetailsAsync(int eventId, bool includeDeleted = false);
    Task<PagedResult<Event>> GetEventsAsync(EventFilterCriteria criteria);
    Task<(bool Succeeded, string? ErrorMessage)> SubmitForApprovalAsync(int eventId, string currentUserId, bool isElevatedAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> WithdrawEventApprovalRequestAsync(int eventId, string currentUserId, bool isElevatedAdmin = false);
    Task<(bool Succeeded, string? ErrorMessage)> ApproveEventAsync(int eventId, string adminUserId, bool isSuperAdmin = false);
    Task<(bool Succeeded, string? ErrorMessage)> RejectEventAsync(int eventId, string adminUserId, string rejectionReason, bool isSuperAdmin = false);
    Task<(bool Succeeded, string? ErrorMessage)> PublishEventAsync(int eventId, string currentUserId, bool isElevatedAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> CancelEventAsync(int eventId, string currentUserId, string reason, bool isElevatedAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> AssignCoordinatorsAsync(int eventId, List<string> coordinatorUserIds, string? primaryCoordinatorUserId, string currentUserId, bool isElevatedAdmin);
    Task<bool> CanUserEditEventAsync(int eventId, string userId, bool isElevatedAdmin);
    Task<bool> CanUserApproveEventAsync(int eventId, string userId, bool isSuperAdmin = false);
}


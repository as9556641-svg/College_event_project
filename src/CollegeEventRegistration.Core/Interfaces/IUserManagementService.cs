using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

public interface IUserManagementService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(UserFilterCriteria criteria, string currentUserId, bool isSuperAdmin);
    Task<UserDetailsDto?> GetUserDetailsAsync(string userId, string currentUserId, bool isSuperAdmin);
    Task<(bool Succeeded, string? ErrorMessage, string? CreatedUserId)> CreateUserAsync(CreateUserDto dto, string currentUserId, bool isSuperAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> UpdateUserAsync(string userId, UpdateUserDto dto, string currentUserId, bool isSuperAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> ToggleUserStatusAsync(string userId, string currentUserId, bool isSuperAdmin);
    Task<(bool Succeeded, string? ErrorMessage)> ChangeUserRoleAsync(string userId, string newRole, string currentUserId, bool isSuperAdmin);
}

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service interface for verifying and enforcing Super Admin immutability and protection policies.
/// </summary>
public interface ISuperAdminProtectionService
{
    Task ValidateUserModificationAsync(string userId, bool newIsActive, DateTimeOffset? newLockoutEnd);
    Task ValidateUserDeletionAsync(string userId);
    Task ValidateRoleRemovalAsync(string userId, string roleName);
}

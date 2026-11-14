using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Exceptions;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Server-side validation service that protects the primary Super Admin account
/// from deletion, deactivation, lockout, or role removal.
/// </summary>
public class SuperAdminProtectionService : ISuperAdminProtectionService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<SuperAdminProtectionService> _logger;

    public SuperAdminProtectionService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<SuperAdminProtectionService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task ValidateUserModificationAsync(string userId, bool newIsActive, DateTimeOffset? newLockoutEnd)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsSuperAdminProtected)
        {
            return;
        }

        if (!newIsActive)
        {
            _logger.LogWarning("Security violation attempt: Cannot deactivate protected Super Admin user {UserId}", userId);
            throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be deactivated.");
        }

        if (newLockoutEnd.HasValue && newLockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Security violation attempt: Cannot lock out protected Super Admin user {UserId}", userId);
            throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be locked out.");
        }
    }

    public async Task ValidateUserDeletionAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsSuperAdminProtected)
        {
            return;
        }

        _logger.LogWarning("Security violation attempt: Cannot delete protected Super Admin user {UserId}", userId);
        throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be deleted.");
    }

    public async Task ValidateRoleRemovalAsync(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsSuperAdminProtected)
        {
            return;
        }

        if (string.Equals(roleName, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Security violation attempt: Cannot remove SuperAdmin role from protected user {UserId}", userId);
            throw new SuperAdminProtectionException("The SuperAdmin role cannot be removed from the protected Super Admin account.");
        }
    }
}


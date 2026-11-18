using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class UserManagementService : IUserManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ISuperAdminProtectionService _protectionService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ISuperAdminProtectionService protectionService,
        IAuditLogService auditLog,
        ILogger<UserManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _protectionService = protectionService;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(UserFilterCriteria criteria, string currentUserId, bool isSuperAdmin)
    {
        // Get all users with query filters
        IQueryable<ApplicationUser> query = _userManager.Users.AsNoTracking();

        // If not SuperAdmin, strictly restrict to Coordinator and Student roles
        if (!isSuperAdmin)
        {
            var elevatedRoleIds = await _context.Roles
                .Where(r => r.Name == nameof(UserRole.SuperAdmin) || r.Name == nameof(UserRole.Admin))
                .Select(r => r.Id)
                .ToListAsync();

            var elevatedUserIds = await _context.UserRoles
                .Where(ur => elevatedRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            query = query.Where(u => !elevatedUserIds.Contains(u.Id) && !u.IsSuperAdminProtected);
        }

        // Search term filter
        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                (u.RollNumber != null && u.RollNumber.ToLower().Contains(term)) ||
                (u.Department != null && u.Department.ToLower().Contains(term)));
        }

        // Active filter
        if (criteria.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == criteria.IsActive.Value);
        }

        // Department filter
        if (!string.IsNullOrWhiteSpace(criteria.Department))
        {
            query = query.Where(u => u.Department == criteria.Department.Trim());
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(criteria.Role))
        {
            var targetRole = await _roleManager.FindByNameAsync(criteria.Role.Trim());
            if (targetRole != null)
            {
                var roleUserIds = await _context.UserRoles
                    .Where(ur => ur.RoleId == targetRole.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                query = query.Where(u => roleUserIds.Contains(u.Id));
            }
        }

        var totalCount = await query.CountAsync();
        var pageSize = Math.Max(1, criteria.PageSize);
        var pageNumber = Math.Max(1, criteria.PageNumber);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();

        // Fetch roles for retrieved users in a single joined query
        var userRoles = await (from ur in _context.UserRoles
                               join r in _context.Roles on ur.RoleId equals r.Id
                               where userIds.Contains(ur.UserId)
                               select new { ur.UserId, RoleName = r.Name })
                               .ToListAsync();

        var rolesByUser = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList());

        var items = users.Select(u => new UserListItemDto
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email ?? string.Empty,
            FirstName = u.FirstName,
            LastName = u.LastName,
            RollNumber = u.RollNumber,
            CollegeName = u.CollegeName,
            Department = u.Department,
            YearOfStudy = u.YearOfStudy,
            CreatedAt = u.CreatedAt,
            IsActive = u.IsActive,
            IsSuperAdminProtected = u.IsSuperAdminProtected,
            Roles = rolesByUser.TryGetValue(u.Id, out var rList) ? rList : new List<string>()
        }).ToList();

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserDetailsDto?> GetUserDetailsAsync(string userId, string currentUserId, bool isSuperAdmin)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Enforce boundary: Admin cannot view Admin or SuperAdmin accounts
        if (!isSuperAdmin)
        {
            if (roles.Contains(nameof(UserRole.SuperAdmin)) || roles.Contains(nameof(UserRole.Admin)) || user.IsSuperAdminProtected)
            {
                _logger.LogWarning("Admin {AdminId} attempted to access privileged user {TargetUserId}", currentUserId, userId);
                return null;
            }
        }

        var createdCount = await _context.Events.CountAsync(e => e.CreatedByUserId == userId && !e.IsDeleted);
        var coordinatedCount = await _context.EventCoordinators.CountAsync(ec => ec.UserId == userId);
        var regCount = await _context.EventRegistrations.CountAsync(r => r.UserId == userId);
        var recentAuditLogs = await _context.AuditLogs
            .Where(a => a.UserId == userId || (a.EntityName == "ApplicationUser" && a.EntityId == userId))
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync();

        return new UserDetailsDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            RollNumber = user.RollNumber,
            CollegeName = user.CollegeName,
            Department = user.Department,
            YearOfStudy = user.YearOfStudy,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive,
            IsSuperAdminProtected = user.IsSuperAdminProtected,
            PhoneNumber = user.PhoneNumber,
            LockoutEnd = user.LockoutEnd,
            Roles = roles,
            CreatedEventsCount = createdCount,
            CoordinatedEventsCount = coordinatedCount,
            RegistrationsCount = regCount,
            RecentAuditLogs = recentAuditLogs
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, string? CreatedUserId)> CreateUserAsync(CreateUserDto dto, string currentUserId, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return (false, "Email address is required.", null);

        if (string.IsNullOrWhiteSpace(dto.Password))
            return (false, "A temporary or initial password is required.", null);

        if (string.IsNullOrWhiteSpace(dto.Role))
            return (false, "User role must be specified.", null);

        var normalizedRole = dto.Role.Trim();

        // Boundary check: Admin can only create Coordinator or Student accounts
        if (!isSuperAdmin)
        {
            if (string.Equals(normalizedRole, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedRole, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Privilege escalation attempt by {UserId} to create role {Role}", currentUserId, normalizedRole);
                return (false, "Access Denied: Admins may only create Coordinator or Student accounts.", null);
            }
        }

        if (!await _roleManager.RoleExistsAsync(normalizedRole))
            return (false, $"Specified role '{normalizedRole}' does not exist.", null);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (existingUser != null)
            return (false, $"A user account with email '{dto.Email.Trim()}' already exists.", null);

        var user = new ApplicationUser
        {
            UserName = dto.Email.Trim(),
            Email = dto.Email.Trim(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Department = dto.Department?.Trim(),
            CollegeName = string.IsNullOrWhiteSpace(dto.CollegeName) ? "VAP UNIVERSITY" : dto.CollegeName.Trim(),
            RollNumber = dto.RollNumber?.Trim(),
            YearOfStudy = dto.YearOfStudy,
            PhoneNumber = dto.PhoneNumber?.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var err = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return (false, err, null);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, normalizedRole);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return (false, "Failed to assign role to newly created user.", null);
        }

        await _auditLog.LogAsync("CreateUser", "ApplicationUser", user.Id, currentUserId,
            newValues: $"Email={user.Email}, Role={normalizedRole}, Name={user.FullName}");

        _logger.LogInformation("User {Email} created with role {Role} by {AdminId}", user.Email, normalizedRole, currentUserId);
        return (true, null, user.Id);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> UpdateUserAsync(string userId, UpdateUserDto dto, string currentUserId, bool isSuperAdmin)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "User not found.");

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Boundary check: Admin cannot touch Admin or SuperAdmin accounts
        if (!isSuperAdmin)
        {
            if (currentRoles.Contains(nameof(UserRole.SuperAdmin)) || currentRoles.Contains(nameof(UserRole.Admin)) || user.IsSuperAdminProtected)
            {
                return (false, "Access Denied: You do not have permission to modify administrative user accounts.");
            }
        }

        // SuperAdmin protection check
        if (user.IsSuperAdminProtected && !dto.IsActive)
        {
            return (false, "The primary Super Admin account is system-protected and cannot be deactivated.");
        }

        var oldValues = $"Name={user.FullName}, Email={user.Email}, Active={user.IsActive}, Dept={user.Department}";

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Department = dto.Department?.Trim();
        user.CollegeName = dto.CollegeName?.Trim();
        user.RollNumber = dto.RollNumber?.Trim();
        user.YearOfStudy = dto.YearOfStudy;
        user.PhoneNumber = dto.PhoneNumber?.Trim();

        // Enforce active status changes safely
        if (user.IsActive != dto.IsActive)
        {
            if (user.Id == currentUserId && !dto.IsActive)
            {
                return (false, "You cannot deactivate your own account.");
            }

            if (!dto.IsActive && currentRoles.Contains(nameof(UserRole.SuperAdmin)))
            {
                var activeSuperAdmins = (await _userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin)))
                    .Count(u => u.IsActive && u.Id != user.Id);
                if (activeSuperAdmins < 1)
                {
                    return (false, "Cannot deactivate the last remaining active Super Admin account.");
                }
            }

            user.IsActive = dto.IsActive;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var err = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return (false, err);
        }

        await _auditLog.LogAsync("UpdateUser", "ApplicationUser", user.Id, currentUserId,
            oldValues: oldValues,
            newValues: $"Name={user.FullName}, Email={user.Email}, Active={user.IsActive}, Dept={user.Department}");

        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ToggleUserStatusAsync(string userId, string currentUserId, bool isSuperAdmin)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "User not found.");

        if (user.Id == currentUserId)
            return (false, "Security rule: You cannot deactivate your own administrative account.");

        var roles = await _userManager.GetRolesAsync(user);

        // Boundary check: Admin cannot toggle Admin or SuperAdmin accounts
        if (!isSuperAdmin)
        {
            if (roles.Contains(nameof(UserRole.SuperAdmin)) || roles.Contains(nameof(UserRole.Admin)) || user.IsSuperAdminProtected)
            {
                return (false, "Access Denied: Admins cannot activate or deactivate administrator accounts.");
            }
        }

        // If currently active and deactivating, perform protection checks
        if (user.IsActive)
        {
            if (user.IsSuperAdminProtected)
            {
                return (false, "The primary Super Admin account is system-protected and cannot be deactivated.");
            }

            if (roles.Contains(nameof(UserRole.SuperAdmin)))
            {
                var activeSuperAdmins = (await _userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin)))
                    .Count(u => u.IsActive && u.Id != user.Id);
                if (activeSuperAdmins < 1)
                {
                    return (false, "Operation rejected: Cannot deactivate the last remaining active Super Admin account.");
                }
            }
        }

        user.IsActive = !user.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        var actionName = user.IsActive ? "ActivateUser" : "DeactivateUser";
        await _auditLog.LogAsync(actionName, "ApplicationUser", user.Id, currentUserId,
            newValues: $"IsActive={user.IsActive}");

        _logger.LogInformation("User {UserId} status toggled to Active={IsActive} by {AdminId}", user.Id, user.IsActive, currentUserId);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ChangeUserRoleAsync(string userId, string newRole, string currentUserId, bool isSuperAdmin)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "User not found.");

        var normalizedRole = newRole.Trim();
        if (!await _roleManager.RoleExistsAsync(normalizedRole))
            return (false, $"Role '{normalizedRole}' does not exist.");

        var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();

        // If already in target role, nothing to change
        if (currentRoles.Contains(normalizedRole))
            return (true, null);

        // Boundary check for Admin
        if (!isSuperAdmin)
        {
            // Admin can only assign Coordinator or Student
            if (normalizedRole != nameof(UserRole.Coordinator) && normalizedRole != nameof(UserRole.Student))
            {
                _logger.LogWarning("Privilege escalation violation by Admin {AdminId} attempting to assign {Role}", currentUserId, normalizedRole);
                return (false, "Access Denied: Admins may only assign Coordinator or Student roles.");
            }

            // Target user must not currently be Admin or SuperAdmin
            if (currentRoles.Contains(nameof(UserRole.SuperAdmin)) || currentRoles.Contains(nameof(UserRole.Admin)) || user.IsSuperAdminProtected)
            {
                return (false, "Access Denied: You do not have permission to modify roles for administrator accounts.");
            }
        }

        // Boundary check for SuperAdmin
        if (user.IsSuperAdminProtected && currentRoles.Contains(nameof(UserRole.SuperAdmin)) && normalizedRole != nameof(UserRole.SuperAdmin))
        {
            return (false, "The primary Super Admin account is system-protected. Its SuperAdmin role cannot be changed.");
        }

        if (user.Id == currentUserId && currentRoles.Contains(nameof(UserRole.SuperAdmin)) && normalizedRole != nameof(UserRole.SuperAdmin))
        {
            return (false, "Self-demotion prevention: You cannot remove your own SuperAdmin privileges.");
        }

        // Ensure at least one active SuperAdmin remains
        if (currentRoles.Contains(nameof(UserRole.SuperAdmin)) && normalizedRole != nameof(UserRole.SuperAdmin))
        {
            var activeSuperAdmins = (await _userManager.GetUsersInRoleAsync(nameof(UserRole.SuperAdmin)))
                .Count(u => u.IsActive && u.Id != user.Id);
            if (activeSuperAdmins < 1)
            {
                return (false, "Cannot remove SuperAdmin role from the last remaining Super Admin in the system.");
            }
        }

        // Replace roles
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            return (false, string.Join("; ", removeResult.Errors.Select(e => e.Description)));
        }

        var addResult = await _userManager.AddToRoleAsync(user, normalizedRole);
        if (!addResult.Succeeded)
        {
            // Try rollback
            await _userManager.AddToRolesAsync(user, currentRoles);
            return (false, string.Join("; ", addResult.Errors.Select(e => e.Description)));
        }

        await _auditLog.LogAsync("ChangeUserRole", "ApplicationUser", user.Id, currentUserId,
            oldValues: $"Roles={string.Join(",", currentRoles)}",
            newValues: $"Role={normalizedRole}");

        _logger.LogInformation("User {UserId} role changed to {Role} by {AdminId}", user.Id, normalizedRole, currentUserId);
        return (true, null);
    }
}

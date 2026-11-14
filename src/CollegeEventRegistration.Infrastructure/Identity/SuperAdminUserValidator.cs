using CollegeEventRegistration.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace CollegeEventRegistration.Infrastructure.Identity;

/// <summary>
/// Custom ASP.NET Core Identity user validator that enforces immutable protection
/// for users marked with IsSuperAdminProtected.
/// </summary>
public class SuperAdminUserValidator : IUserValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        if (user.IsSuperAdminProtected)
        {
            var errors = new List<IdentityError>();

            if (!user.IsActive)
            {
                errors.Add(new IdentityError
                {
                    Code = "ProtectedSuperAdminCannotBeDeactivated",
                    Description = "The primary Super Admin account is system-protected and cannot be deactivated."
                });
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                errors.Add(new IdentityError
                {
                    Code = "ProtectedSuperAdminCannotBeLockedOut",
                    Description = "The primary Super Admin account is system-protected and cannot be locked out."
                });
            }

            if (errors.Count > 0)
            {
                return Task.FromResult(IdentityResult.Failed(errors.ToArray()));
            }
        }

        return Task.FromResult(IdentityResult.Success);
    }
}


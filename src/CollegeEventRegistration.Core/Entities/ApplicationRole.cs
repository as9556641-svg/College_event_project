using Microsoft.AspNetCore.Identity;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Application role extending ASP.NET Core IdentityRole.
/// </summary>
public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationRole() : base() { }

    public ApplicationRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
    }
}


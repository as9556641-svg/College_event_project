using Microsoft.AspNetCore.Identity;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Core IdentityUser with college and profile attributes.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? RollNumber { get; set; }
    public string? CollegeName { get; set; }
    public string? Department { get; set; }
    public int? YearOfStudy { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
}

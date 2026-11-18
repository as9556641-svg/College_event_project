using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

public class UserFilterCriteria
{
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Department { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

public class UserListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? RollNumber { get; set; }
    public string? CollegeName { get; set; }
    public string? Department { get; set; }
    public int? YearOfStudy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsSuperAdminProtected { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class UserDetailsDto : UserListItemDto
{
    public string? PhoneNumber { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int CreatedEventsCount { get; set; }
    public int CoordinatedEventsCount { get; set; }
    public int RegistrationsCount { get; set; }
    public List<AuditLog> RecentAuditLogs { get; set; } = new();
}

public class CreateUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? CollegeName { get; set; }
    public string? RollNumber { get; set; }
    public int? YearOfStudy { get; set; }
    public string? PhoneNumber { get; set; }
}

public class UpdateUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? CollegeName { get; set; }
    public string? RollNumber { get; set; }
    public int? YearOfStudy { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
}

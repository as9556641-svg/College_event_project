using System.ComponentModel.DataAnnotations;
using CollegeEventRegistration.Core.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeEventRegistration.Web.Areas.Admin.Models;

public class UserListViewModel
{
    public PagedResult<UserListItemDto> PagedUsers { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; } // "all", "active", "inactive"
    public string? Department { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int TotalCount { get; set; }
    public List<SelectListItem> RolesList { get; set; } = new();
    public List<SelectListItem> DepartmentsList { get; set; } = new();
}

public class UserDetailsViewModel
{
    public UserDetailsDto User { get; set; } = null!;
    public bool IsSuperAdmin { get; set; }
    public bool CanChangeRole { get; set; }
    public bool CanToggleStatus { get; set; }
    public bool CanEdit { get; set; }
    public List<SelectListItem> AvailableRoles { get; set; } = new();
}

public class UserCreateViewModel
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Institutional email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Initial password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role selection is required.")]
    [Display(Name = "Assigned Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Department / School")]
    public string? Department { get; set; }

    [Display(Name = "College / Institution")]
    public string? CollegeName { get; set; } = "VAP UNIVERSITY";

    [Display(Name = "Student Roll Number / Staff ID")]
    public string? RollNumber { get; set; }

    [Range(1, 6, ErrorMessage = "Year of study must be between 1 and 6.")]
    [Display(Name = "Year of Study (If student)")]
    public int? YearOfStudy { get; set; }

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Contact Phone Number")]
    public string? PhoneNumber { get; set; }

    public List<SelectListItem> AvailableRoles { get; set; } = new();
    public List<SelectListItem> AvailableDepartments { get; set; } = new();
}

public class UserEditViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Email Address (Read-only)")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Current Role")]
    public string CurrentRole { get; set; } = string.Empty;

    [Display(Name = "Change Assigned Role")]
    public string? NewRole { get; set; }

    [Display(Name = "Department / School")]
    public string? Department { get; set; }

    [Display(Name = "College / Institution")]
    public string? CollegeName { get; set; }

    [Display(Name = "Student Roll Number / Staff ID")]
    public string? RollNumber { get; set; }

    [Range(1, 6, ErrorMessage = "Year of study must be between 1 and 6.")]
    [Display(Name = "Year of Study")]
    public int? YearOfStudy { get; set; }

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Contact Phone Number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Account Active")]
    public bool IsActive { get; set; } = true;

    public bool IsSuperAdminProtected { get; set; }
    public bool CanChangeRole { get; set; }

    public List<SelectListItem> AvailableRoles { get; set; } = new();
    public List<SelectListItem> AvailableDepartments { get; set; } = new();
}

public class ChangeRoleRequestModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target role is required.")]
    public string NewRole { get; set; } = string.Empty;
}

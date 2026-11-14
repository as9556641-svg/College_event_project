using System.ComponentModel.DataAnnotations;

namespace CollegeEventRegistration.Web.Models.Account;

/// <summary>
/// View model for student registration.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "College / Personal Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select your department.")]
    [Display(Name = "Department")]
    public string Department { get; set; } = string.Empty;

    [Display(Name = "Student Roll / ID Number")]
    public string? RollNumber { get; set; }

    [Display(Name = "Year of Study")]
    [Range(1, 5, ErrorMessage = "Year of study must be between 1 and 5.")]
    public int? YearOfStudy { get; set; } = 1;

    [Display(Name = "College / Institution Name")]
    public string CollegeName { get; set; } = "Central University";

    public string? ReturnUrl { get; set; }
}


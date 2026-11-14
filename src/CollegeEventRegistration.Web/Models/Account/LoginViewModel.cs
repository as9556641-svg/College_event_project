using System.ComponentModel.DataAnnotations;

namespace CollegeEventRegistration.Web.Models.Account;

/// <summary>
/// View model for authenticating users into the system.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your college email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "College Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember this device")]
    public bool RememberMe { get; set; } = false;

    public string? ReturnUrl { get; set; }
}


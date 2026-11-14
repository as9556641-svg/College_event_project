using System.ComponentModel.DataAnnotations;

namespace CollegeEventRegistration.Web.Models.Account;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Please enter your registered email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Registered Email Address")]
    public string Email { get; set; } = string.Empty;
}


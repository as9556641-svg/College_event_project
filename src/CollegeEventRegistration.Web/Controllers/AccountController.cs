using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Controllers;

/// <summary>
/// Handles authentication, user registration, role-based redirection, and session lifecycle.
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // LOGIN (GET / POST)
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // If already logged in, redirect directly to role dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                return await RedirectToRoleDashboardAsync(currentUser, returnUrl);
            }
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found for email {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Invalid email or password. Please verify your credentials.");
            return View(model);
        }

        // Account active check
        if (!user.IsActive)
        {
            _logger.LogWarning("Login blocked: Inactive account {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact the college administrator.");
            return View(model);
        }

        // Real ASP.NET Core Identity authentication with lockout protection
        var result = await _signInManager.PasswordSignInAsync(
            user.UserName ?? model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully.", model.Email);
            return await RedirectToRoleDashboardAsync(user, returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User {Email} account locked out.", model.Email);
            ModelState.AddModelError(string.Empty, "Your account has been temporarily locked due to multiple failed login attempts. Please try again later.");
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToAction("LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
        }

        _logger.LogWarning("Login failed for {Email}: Invalid password.", model.Email);
        ModelState.AddModelError(string.Empty, "Invalid email or password. Please verify your credentials.");
        return View(model);
    }

    // -------------------------------------------------------------------------
    // REGISTER STUDENT (GET / POST)
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                return await RedirectToRoleDashboardAsync(currentUser, returnUrl);
            }
        }

        ViewData["Departments"] = await _context.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Departments"] = await _context.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var student = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Department = model.Department,
            RollNumber = model.RollNumber?.Trim(),
            YearOfStudy = model.YearOfStudy,
            CollegeName = model.CollegeName.Trim(),
            EmailConfirmed = true, // Set to true for development flow
            IsActive = true
        };

        var result = await _userManager.CreateAsync(student, model.Password);
        if (result.Succeeded)
        {
            // Assign default Student role
            await _userManager.AddToRoleAsync(student, nameof(UserRole.Student));

            _logger.LogInformation("New student account registered: {Email}", student.Email);

            // Automatically sign in the new student
            await _signInManager.SignInAsync(student, isPersistent: false);

            return await RedirectToRoleDashboardAsync(student, returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // -------------------------------------------------------------------------
    // LOGOUT (POST)
    // -------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }

    // -------------------------------------------------------------------------
    // ACCESS DENIED (GET)
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // -------------------------------------------------------------------------
    // FORGOT PASSWORD (GET / POST)
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            // Do not reveal whether user exists
            ViewBag.Message = "If your email is registered in our portal, a password reset link has been dispatched.";
            return View("ForgotPasswordConfirmation");
        }

        // Simulation for now until SMTP is configured in subsequent phase
        ViewBag.Message = "If your email is registered in our portal, a password reset link has been dispatched.";
        return View("ForgotPasswordConfirmation");
    }

    // -------------------------------------------------------------------------
    // ROLE-BASED REDIRECTION HELPER
    // -------------------------------------------------------------------------
    private async Task<IActionResult> RedirectToRoleDashboardAsync(ApplicationUser user, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(nameof(UserRole.SuperAdmin)))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });
        }
        if (roles.Contains(nameof(UserRole.Admin)))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
        if (roles.Contains(nameof(UserRole.Coordinator)))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Coordinator" });
        }
        if (roles.Contains(nameof(UserRole.Student)))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Student" });
        }

        return RedirectToAction("Index", "Home");
    }
}


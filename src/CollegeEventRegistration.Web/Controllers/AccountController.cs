using System.Security.Claims;
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

        if (TempData["ErrorMessage"] != null)
        {
            ModelState.AddModelError(string.Empty, TempData["ErrorMessage"]!.ToString()!);
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
    // EXTERNAL AUTHENTICATION (GOOGLE OAUTH)
    // -------------------------------------------------------------------------
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogWarning("External authentication provider returned error: {RemoteError}", remoteError);
            TempData["ErrorMessage"] = $"External login error: {remoteError}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Failed to retrieve external login info from provider.");
            TempData["ErrorMessage"] = "Could not authenticate with Google. Please try again or use email credentials.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // 1. Sign in if user already linked this external login
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            var linkedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (linkedUser != null)
            {
                if (!linkedUser.IsActive)
                {
                    await _signInManager.SignOutAsync();
                    _logger.LogWarning("External login blocked: Deactivated account {Email}", linkedUser.Email);
                    TempData["ErrorMessage"] = "Your account has been deactivated. Please contact the college administrator.";
                    return RedirectToAction(nameof(Login), new { returnUrl });
                }

                _logger.LogInformation("User {Email} logged in via {Provider}.", linkedUser.Email, info.LoginProvider);
                return await RedirectToRoleDashboardAsync(linkedUser, returnUrl);
            }
        }

        if (signInResult.IsLockedOut)
        {
            _logger.LogWarning("External login blocked: Account is locked out.");
            TempData["ErrorMessage"] = "Your account has been temporarily locked due to multiple failed login attempts. Please try again later.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // 2. Extract verified email claim from external principal
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("External login error: No email claim provided by {Provider}.", info.LoginProvider);
            TempData["ErrorMessage"] = "Google did not provide a verified email address.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // 3. Check if user already exists with this email address
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            if (!user.IsActive)
            {
                _logger.LogWarning("External account linking blocked: Deactivated account {Email}", user.Email);
                TempData["ErrorMessage"] = "Your account has been deactivated. Please contact the college administrator.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            // Link Google external login to existing user account (preserves existing roles and data)
            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                _logger.LogWarning("Failed to link {Provider} to user {Email}: {Errors}",
                    info.LoginProvider,
                    email,
                    string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                TempData["ErrorMessage"] = "Could not link your Google login to the existing account. Please sign in with your password.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            _logger.LogInformation("Successfully linked {Provider} login to existing user {Email}.", info.LoginProvider, email);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return await RedirectToRoleDashboardAsync(user, returnUrl);
        }

        // 4. Provision new user account (Strict Requirement: Assign Student role ONLY, never privileged roles)
        var givenName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
        var surname = info.Principal.FindFirstValue(ClaimTypes.Surname);
        var fullName = info.Principal.FindFirstValue(ClaimTypes.Name);

        string firstName = !string.IsNullOrWhiteSpace(givenName)
            ? givenName.Trim()
            : (!string.IsNullOrWhiteSpace(fullName) ? fullName.Split(' ')[0].Trim() : "Student");

        string lastName = !string.IsNullOrWhiteSpace(surname)
            ? surname.Trim()
            : (!string.IsNullOrWhiteSpace(fullName) && fullName.Contains(' ')
                ? string.Join(" ", fullName.Split(' ').Skip(1)).Trim()
                : "User");

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CollegeName = "VAP UNIVERSITY",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
        {
            _logger.LogError("Failed to create user from {Provider} login: {Errors}",
                info.LoginProvider,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            TempData["ErrorMessage"] = "Failed to create a student account from your Google profile.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        // Assign default Student role (NEVER privileged roles)
        await _userManager.AddToRoleAsync(newUser, nameof(UserRole.Student));

        // Add external login mapping
        await _userManager.AddLoginAsync(newUser, info);

        _logger.LogInformation("Created new Student account for {Email} via {Provider}.", email, info.LoginProvider);
        await _signInManager.SignInAsync(newUser, isPersistent: false);
        return await RedirectToRoleDashboardAsync(newUser, returnUrl);
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


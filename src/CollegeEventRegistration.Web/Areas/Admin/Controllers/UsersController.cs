using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class UsersController : Controller
{
    private readonly IUserManagementService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UsersController> _logger;

    private static readonly List<string> PermittedAdminRoles = new()
    {
        nameof(UserRole.Coordinator),
        nameof(UserRole.Student)
    };

    public UsersController(
        IUserManagementService userService,
        IDepartmentService departmentService,
        UserManager<ApplicationUser> userManager,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _departmentService = departmentService;
        _userManager = userManager;
        _logger = logger;
    }

    // GET: /Admin/Users
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? role,
        string? status,
        string? department,
        int pageNumber = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Enforce role filter within PermittedAdminRoles
        if (!string.IsNullOrWhiteSpace(role) && !PermittedAdminRoles.Contains(role))
        {
            role = null;
        }

        bool? isActiveFilter = status?.ToLowerInvariant() switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };

        var criteria = new UserFilterCriteria
        {
            SearchTerm = searchTerm,
            Role = role,
            IsActive = isActiveFilter,
            Department = department,
            PageNumber = pageNumber,
            PageSize = 15
        };

        var pagedUsers = await _userService.GetUsersAsync(criteria, currentUser.Id, isSuperAdmin: false);
        var departments = await _departmentService.GetAllDepartmentsAsync();

        var viewModel = new UserListViewModel
        {
            PagedUsers = pagedUsers,
            SearchTerm = searchTerm,
            Role = role,
            Status = status?.ToLowerInvariant() ?? "all",
            Department = department,
            IsSuperAdmin = false,
            TotalCount = pagedUsers.TotalCount,
            RolesList = PermittedAdminRoles.Select(r => new SelectListItem(r, r, r == role)).ToList(),
            DepartmentsList = departments.Select(d => new SelectListItem(d.Name, d.Name, d.Name == department)).ToList()
        };

        return View(viewModel);
    }

    // GET: /Admin/Users/Details/{id}
    public async Task<IActionResult> Details(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var userDetails = await _userService.GetUserDetailsAsync(id, currentUser.Id, isSuperAdmin: false);
        if (userDetails == null) return NotFound();

        var viewModel = new UserDetailsViewModel
        {
            User = userDetails,
            IsSuperAdmin = false,
            CanChangeRole = true,
            CanToggleStatus = true,
            CanEdit = true,
            AvailableRoles = PermittedAdminRoles.Select(r => new SelectListItem(r, r)).ToList()
        };

        return View(viewModel);
    }

    // GET: /Admin/Users/Create
    public async Task<IActionResult> Create()
    {
        var model = new UserCreateViewModel();
        await PopulateCreateLookupsAsync(model);
        return View(model);
    }

    // POST: /Admin/Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Enforce role boundary: Admin cannot create Admin or SuperAdmin
        if (!PermittedAdminRoles.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Administrators may only create Coordinator or Student accounts.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCreateLookupsAsync(model);
            return View(model);
        }

        var dto = new CreateUserDto
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Email = model.Email.Trim(),
            Password = model.Password,
            Role = model.Role.Trim(),
            Department = model.Department?.Trim(),
            CollegeName = model.CollegeName?.Trim(),
            RollNumber = model.RollNumber?.Trim(),
            YearOfStudy = model.YearOfStudy,
            PhoneNumber = model.PhoneNumber?.Trim()
        };

        var result = await _userService.CreateUserAsync(dto, currentUser.Id, isSuperAdmin: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create user account.");
            await PopulateCreateLookupsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Account '{dto.Email}' successfully created as '{dto.Role}'.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Users/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var userDetails = await _userService.GetUserDetailsAsync(id, currentUser.Id, isSuperAdmin: false);
        if (userDetails == null) return NotFound();

        var currentRole = userDetails.Roles.FirstOrDefault() ?? string.Empty;

        var model = new UserEditViewModel
        {
            Id = userDetails.Id,
            FirstName = userDetails.FirstName,
            LastName = userDetails.LastName,
            Email = userDetails.Email,
            Department = userDetails.Department,
            CollegeName = userDetails.CollegeName,
            RollNumber = userDetails.RollNumber,
            YearOfStudy = userDetails.YearOfStudy,
            PhoneNumber = userDetails.PhoneNumber,
            IsActive = userDetails.IsActive,
            CurrentRole = currentRole,
            NewRole = currentRole,
            IsSuperAdminProtected = false,
            CanChangeRole = true
        };

        await PopulateEditLookupsAsync(model);
        return View(model);
    }

    // POST: /Admin/Users/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateEditLookupsAsync(model);
            return View(model);
        }

        var dto = new UpdateUserDto
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Email = model.Email.Trim(),
            Department = model.Department?.Trim(),
            CollegeName = model.CollegeName?.Trim(),
            RollNumber = model.RollNumber?.Trim(),
            YearOfStudy = model.YearOfStudy,
            PhoneNumber = model.PhoneNumber?.Trim(),
            IsActive = model.IsActive
        };

        var updateResult = await _userService.UpdateUserAsync(id, dto, currentUser.Id, isSuperAdmin: false);
        if (!updateResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, updateResult.ErrorMessage ?? "Failed to update user profile.");
            await PopulateEditLookupsAsync(model);
            return View(model);
        }

        // Process role change if requested and permitted
        if (!string.IsNullOrWhiteSpace(model.NewRole) && model.NewRole != model.CurrentRole)
        {
            if (!PermittedAdminRoles.Contains(model.NewRole))
            {
                TempData["ErrorMessage"] = "Administrators may only assign Coordinator or Student roles.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var roleResult = await _userService.ChangeUserRoleAsync(id, model.NewRole.Trim(), currentUser.Id, isSuperAdmin: false);
            if (!roleResult.Succeeded)
            {
                TempData["ErrorMessage"] = $"Profile updated, but role assignment failed: {roleResult.ErrorMessage}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        TempData["SuccessMessage"] = $"User profile for '{dto.FirstName} {dto.LastName}' successfully updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Users/ToggleStatus/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _userService.ToggleUserStatusAsync(id, currentUser.Id, isSuperAdmin: false);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Account status successfully updated.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Users/ChangeRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeRoleRequestModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!PermittedAdminRoles.Contains(model.NewRole))
        {
            TempData["ErrorMessage"] = "Administrators may only assign Coordinator or Student roles.";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }

        var result = await _userService.ChangeUserRoleAsync(model.UserId, model.NewRole, currentUser.Id, isSuperAdmin: false);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = $"User role successfully changed to '{model.NewRole}'.";
        }

        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    private async Task PopulateCreateLookupsAsync(UserCreateViewModel model)
    {
        var departments = await _departmentService.GetAllDepartmentsAsync();

        model.AvailableRoles = PermittedAdminRoles
            .Select(r => new SelectListItem(r, r, r == model.Role))
            .ToList();

        model.AvailableDepartments = departments
            .Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Name, d.Name == model.Department))
            .ToList();
    }

    private async Task PopulateEditLookupsAsync(UserEditViewModel model)
    {
        var departments = await _departmentService.GetAllDepartmentsAsync();

        model.AvailableRoles = PermittedAdminRoles
            .Select(r => new SelectListItem(r, r, r == model.NewRole))
            .ToList();

        model.AvailableDepartments = departments
            .Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Name, d.Name == model.Department))
            .ToList();
    }
}

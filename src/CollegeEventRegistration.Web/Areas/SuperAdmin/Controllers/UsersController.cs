using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public class UsersController : Controller
{
    private readonly IUserManagementService _userService;
    private readonly IDepartmentService _departmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserManagementService userService,
        IDepartmentService departmentService,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _departmentService = departmentService;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    // GET: /SuperAdmin/Users
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? role,
        string? status,
        string? department,
        int pageNumber = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

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

        var pagedUsers = await _userService.GetUsersAsync(criteria, currentUser.Id, isSuperAdmin: true);
        var departments = await _departmentService.GetAllDepartmentsAsync();
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        var viewModel = new UserListViewModel
        {
            PagedUsers = pagedUsers,
            SearchTerm = searchTerm,
            Role = role,
            Status = status?.ToLowerInvariant() ?? "all",
            Department = department,
            IsSuperAdmin = true,
            TotalCount = pagedUsers.TotalCount,
            RolesList = allRoles.Select(r => new SelectListItem(r, r, r == role)).ToList(),
            DepartmentsList = departments.Select(d => new SelectListItem(d.Name, d.Name, d.Name == department)).ToList()
        };

        return View(viewModel);
    }

    // GET: /SuperAdmin/Users/Details/{id}
    public async Task<IActionResult> Details(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var userDetails = await _userService.GetUserDetailsAsync(id, currentUser.Id, isSuperAdmin: true);
        if (userDetails == null) return NotFound();

        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        var viewModel = new UserDetailsViewModel
        {
            User = userDetails,
            IsSuperAdmin = true,
            CanChangeRole = !userDetails.IsSuperAdminProtected,
            CanToggleStatus = !userDetails.IsSuperAdminProtected && userDetails.Id != currentUser.Id,
            CanEdit = true,
            AvailableRoles = allRoles.Select(r => new SelectListItem(r, r)).ToList()
        };

        return View(viewModel);
    }

    // GET: /SuperAdmin/Users/Create
    public async Task<IActionResult> Create()
    {
        var model = new UserCreateViewModel();
        await PopulateCreateLookupsAsync(model);
        return View(model);
    }

    // POST: /SuperAdmin/Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

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

        var result = await _userService.CreateUserAsync(dto, currentUser.Id, isSuperAdmin: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create user account.");
            await PopulateCreateLookupsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = $"User account '{dto.Email}' successfully created with role '{dto.Role}'.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SuperAdmin/Users/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var userDetails = await _userService.GetUserDetailsAsync(id, currentUser.Id, isSuperAdmin: true);
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
            IsSuperAdminProtected = userDetails.IsSuperAdminProtected,
            CanChangeRole = !userDetails.IsSuperAdminProtected
        };

        await PopulateEditLookupsAsync(model);
        return View(model);
    }

    // POST: /SuperAdmin/Users/Edit/{id}
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

        var updateResult = await _userService.UpdateUserAsync(id, dto, currentUser.Id, isSuperAdmin: true);
        if (!updateResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, updateResult.ErrorMessage ?? "Failed to update user profile.");
            await PopulateEditLookupsAsync(model);
            return View(model);
        }

        // Process role change if permitted and requested
        if (!string.IsNullOrWhiteSpace(model.NewRole) && model.NewRole != model.CurrentRole && model.CanChangeRole)
        {
            var roleResult = await _userService.ChangeUserRoleAsync(id, model.NewRole.Trim(), currentUser.Id, isSuperAdmin: true);
            if (!roleResult.Succeeded)
            {
                TempData["ErrorMessage"] = $"Profile updated, but role change failed: {roleResult.ErrorMessage}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        TempData["SuccessMessage"] = $"User profile for '{dto.FirstName} {dto.LastName}' successfully updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /SuperAdmin/Users/ToggleStatus/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _userService.ToggleUserStatusAsync(id, currentUser.Id, isSuperAdmin: true);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "User account status toggled successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /SuperAdmin/Users/ChangeRole
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeRoleRequestModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _userService.ChangeUserRoleAsync(model.UserId, model.NewRole, currentUser.Id, isSuperAdmin: true);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = $"User role successfully updated to '{model.NewRole}'.";
        }

        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    private async Task PopulateCreateLookupsAsync(UserCreateViewModel model)
    {
        var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        var departments = await _departmentService.GetAllDepartmentsAsync();

        model.AvailableRoles = roles
            .Select(r => new SelectListItem(r, r, r == model.Role))
            .ToList();

        model.AvailableDepartments = departments
            .Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Name, d.Name == model.Department))
            .ToList();
    }

    private async Task PopulateEditLookupsAsync(UserEditViewModel model)
    {
        var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        var departments = await _departmentService.GetAllDepartmentsAsync();

        model.AvailableRoles = roles
            .Select(r => new SelectListItem(r, r, r == model.NewRole))
            .ToList();

        model.AvailableDepartments = departments
            .Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Name, d.Name == model.Department))
            .ToList();
    }
}

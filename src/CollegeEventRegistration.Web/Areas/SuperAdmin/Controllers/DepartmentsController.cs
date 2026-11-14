using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public class DepartmentsController : Controller
{
    private readonly IDepartmentService _departmentService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DepartmentsController(
        IDepartmentService departmentService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _departmentService = departmentService;
        _context = context;
        _userManager = userManager;
    }

    // GET: /SuperAdmin/Departments
    public async Task<IActionResult> Index(string? searchTerm)
    {
        var departments = await _departmentService.GetAllDepartmentsAsync(includeInactive: true, searchTerm: searchTerm);

        var departmentIds = departments.Select(d => d.Id).ToList();

        // Get user counts by matching string Department with department Code or Name
        var users = await _context.Users
            .Where(u => u.Department != null)
            .Select(u => u.Department!)
            .ToListAsync();

        var userCounts = departments.ToDictionary(
            d => d.Id,
            d => users.Count(u => string.Equals(u, d.Code, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(u, d.Name, StringComparison.OrdinalIgnoreCase))
        );

        // Get event counts per department
        var eventCounts = await _context.Events
            .Where(e => !e.IsDeleted && departmentIds.Contains(e.DepartmentId))
            .GroupBy(e => e.DepartmentId)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        var viewModel = new DepartmentIndexViewModel
        {
            SearchTerm = searchTerm,
            Departments = departments.Select(d => new DepartmentItemViewModel
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                HeadOfDepartment = d.HeadOfDepartment,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UserCount = userCounts.TryGetValue(d.Id, out var uc) ? uc : 0,
                EventCount = eventCounts.TryGetValue(d.Id, out var ec) ? ec : 0
            }).ToList()
        };

        return View(viewModel);
    }

    // GET: /SuperAdmin/Departments/Create
    public IActionResult Create()
    {
        return View(new DepartmentCreateViewModel());
    }

    // POST: /SuperAdmin/Departments/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _departmentService.CreateDepartmentAsync(
            model.Name.Trim(),
            model.Code.Trim().ToUpperInvariant(),
            model.Description?.Trim(),
            model.HeadOfDepartment?.Trim()
        );

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create department.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Department '{result.Department!.Name}' ({result.Department.Code}) created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SuperAdmin/Departments/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var department = await _departmentService.GetDepartmentByIdAsync(id);
        if (department == null)
        {
            return NotFound();
        }

        var viewModel = new DepartmentEditViewModel
        {
            Id = department.Id,
            Name = department.Name,
            Code = department.Code,
            Description = department.Description,
            HeadOfDepartment = department.HeadOfDepartment,
            IsActive = department.IsActive
        };

        return View(viewModel);
    }

    // POST: /SuperAdmin/Departments/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DepartmentEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _departmentService.UpdateDepartmentAsync(
            model.Id,
            model.Name.Trim(),
            model.Code.Trim().ToUpperInvariant(),
            model.Description?.Trim(),
            model.HeadOfDepartment?.Trim(),
            model.IsActive
        );

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update department.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Department '{result.Department!.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SuperAdmin/Departments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var department = await _departmentService.GetDepartmentByIdAsync(id);
        if (department == null)
        {
            return NotFound();
        }

        var canDeleteCheck = await _departmentService.CanDeleteDepartmentAsync(id);

        var userCount = await _context.Users
            .CountAsync(u => u.Department == department.Name || u.Department == department.Code);

        var viewModel = new DepartmentDetailsViewModel
        {
            Department = department,
            EventCount = await _context.Events.CountAsync(e => e.DepartmentId == id && !e.IsDeleted),
            UserCount = userCount,
            AssociatedEvents = await _context.Events
                .Where(e => e.DepartmentId == id && !e.IsDeleted)
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync(),
            CanDelete = canDeleteCheck.CanDelete,
            DeleteBlockReason = canDeleteCheck.Reason
        };

        return View(viewModel);
    }

    // POST: /SuperAdmin/Departments/ToggleStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var result = await _departmentService.ToggleStatusAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Department status updated successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /SuperAdmin/Departments/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _departmentService.DeleteDepartmentAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Department deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}

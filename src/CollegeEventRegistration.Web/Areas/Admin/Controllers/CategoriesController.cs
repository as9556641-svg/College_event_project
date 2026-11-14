using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class CategoriesController : Controller
{
    private readonly IEventCategoryService _categoryService;
    private readonly ApplicationDbContext _context;

    public CategoriesController(
        IEventCategoryService categoryService,
        ApplicationDbContext context)
    {
        _categoryService = categoryService;
        _context = context;
    }

    // GET: /Admin/Categories
    public async Task<IActionResult> Index(string? searchTerm)
    {
        var categories = await _categoryService.GetAllCategoriesAsync(includeInactive: true, searchTerm: searchTerm);
        var categoryIds = categories.Select(c => c.Id).ToList();

        var eventCounts = await _context.Events
            .Where(e => !e.IsDeleted && categoryIds.Contains(e.EventCategoryId))
            .GroupBy(e => e.EventCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        var viewModel = new CategoryIndexViewModel
        {
            SearchTerm = searchTerm,
            Categories = categories.Select(c => new CategoryItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                IconClass = c.IconClass,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                EventCount = eventCounts.TryGetValue(c.Id, out var count) ? count : 0
            }).ToList()
        };

        return View(viewModel);
    }

    // GET: /Admin/Categories/Create
    public IActionResult Create()
    {
        return View(new CategoryCreateViewModel());
    }

    // POST: /Admin/Categories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _categoryService.CreateCategoryAsync(
            model.Name.Trim(),
            model.Description?.Trim(),
            model.IconClass?.Trim()
        );

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create category.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Category '{result.Category!.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Admin/Categories/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _categoryService.GetCategoryByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }

        var viewModel = new CategoryEditViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconClass = category.IconClass,
            IsActive = category.IsActive
        };

        return View(viewModel);
    }

    // POST: /Admin/Categories/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _categoryService.UpdateCategoryAsync(
            model.Id,
            model.Name.Trim(),
            model.Description?.Trim(),
            model.IconClass?.Trim(),
            model.IsActive
        );

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update category.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Category '{result.Category!.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/Categories/ToggleStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var result = await _categoryService.ToggleStatusAsync(id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Category status updated successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}

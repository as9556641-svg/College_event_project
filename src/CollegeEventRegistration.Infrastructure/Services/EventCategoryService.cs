using System.Text.RegularExpressions;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class EventCategoryService : IEventCategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<EventCategoryService> _logger;

    public EventCategoryService(
        ApplicationDbContext context,
        IAuditLogService auditLog,
        ILogger<EventCategoryService> logger)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EventCategory>> GetAllCategoriesAsync(bool includeInactive = false, string? searchTerm = null)
    {
        IQueryable<EventCategory> query = _context.EventCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Slug.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<EventCategory?> GetCategoryByIdAsync(int id)
    {
        return await _context.EventCategories
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<EventCategory?> GetCategoryBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var cleanSlug = slug.Trim().ToLowerInvariant();
        return await _context.EventCategories.FirstOrDefaultAsync(c => c.Slug == cleanSlug);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, EventCategory? Category)> CreateCategoryAsync(string name, string? description, string? iconClass)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Category name is required.", null);

        var cleanName = name.Trim();
        var baseSlug = GenerateSlug(cleanName);

        var slug = baseSlug;
        int count = 1;
        while (await _context.EventCategories.AnyAsync(c => c.Slug == slug))
        {
            slug = $"{baseSlug}-{++count}";
        }

        var category = new EventCategory
        {
            Name = cleanName,
            Slug = slug,
            Description = description?.Trim(),
            IconClass = string.IsNullOrWhiteSpace(iconClass) ? "bi-calendar-event" : iconClass.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.EventCategories.Add(category);
        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("CreateCategory", nameof(EventCategory), category.Id.ToString(), null,
            newValues: $"Name={category.Name}, Slug={category.Slug}");

        _logger.LogInformation("Event category created: {Name} ({Slug})", category.Name, category.Slug);
        return (true, null, category);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, EventCategory? Category)> UpdateCategoryAsync(int id, string name, string? description, string? iconClass, bool isActive)
    {
        var category = await _context.EventCategories.FindAsync(id);
        if (category == null)
            return (false, "Category not found.", null);

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Category name is required.", null);

        var cleanName = name.Trim();
        var oldValues = $"Name={category.Name}, IsActive={category.IsActive}";

        category.Name = cleanName;
        category.Description = description?.Trim();
        if (!string.IsNullOrWhiteSpace(iconClass))
        {
            category.IconClass = iconClass.Trim();
        }
        category.IsActive = isActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("UpdateCategory", nameof(EventCategory), category.Id.ToString(), null,
            oldValues: oldValues,
            newValues: $"Name={category.Name}, IsActive={category.IsActive}");

        return (true, null, category);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ToggleStatusAsync(int id)
    {
        var category = await _context.EventCategories.FindAsync(id);
        if (category == null)
            return (false, "Category not found.");

        category.IsActive = !category.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("ToggleCategoryStatus", nameof(EventCategory), category.Id.ToString(), null,
            newValues: $"IsActive={category.IsActive}");

        return (true, null);
    }

    private static string GenerateSlug(string text)
    {
        var str = text.ToLowerInvariant();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", " ").Trim();
        str = Regex.Replace(str, @"\s", "-");
        return string.IsNullOrEmpty(str) ? "event-category" : str;
    }
}


using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

public interface IEventCategoryService
{
    Task<IReadOnlyList<EventCategory>> GetAllCategoriesAsync(bool includeInactive = false, string? searchTerm = null);
    Task<EventCategory?> GetCategoryByIdAsync(int id);
    Task<EventCategory?> GetCategoryBySlugAsync(string slug);
    Task<(bool Succeeded, string? ErrorMessage, EventCategory? Category)> CreateCategoryAsync(string name, string? description, string? iconClass);
    Task<(bool Succeeded, string? ErrorMessage, EventCategory? Category)> UpdateCategoryAsync(int id, string name, string? description, string? iconClass, bool isActive);
    Task<(bool Succeeded, string? ErrorMessage)> ToggleStatusAsync(int id);
}


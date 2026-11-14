using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

public interface IDepartmentService
{
    Task<IReadOnlyList<Department>> GetAllDepartmentsAsync(bool includeInactive = false, string? searchTerm = null);
    Task<Department?> GetDepartmentByIdAsync(int id);
    Task<Department?> GetDepartmentByCodeAsync(string code);
    Task<(bool Succeeded, string? ErrorMessage, Department? Department)> CreateDepartmentAsync(string name, string code, string? description, string? headOfDepartment);
    Task<(bool Succeeded, string? ErrorMessage, Department? Department)> UpdateDepartmentAsync(int id, string name, string code, string? description, string? headOfDepartment, bool isActive);
    Task<(bool Succeeded, string? ErrorMessage)> ToggleStatusAsync(int id);
    Task<(bool CanDelete, string? Reason)> CanDeleteDepartmentAsync(int id);
    Task<(bool Succeeded, string? ErrorMessage)> DeleteDepartmentAsync(int id);
}


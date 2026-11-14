using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class DepartmentService : IDepartmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(
        ApplicationDbContext context,
        IAuditLogService auditLog,
        ILogger<DepartmentService> logger)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Department>> GetAllDepartmentsAsync(bool includeInactive = false, string? searchTerm = null)
    {
        IQueryable<Department> query = _context.Departments.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(d => d.Name.Contains(term) || d.Code.Contains(term) || (d.HeadOfDepartment != null && d.HeadOfDepartment.Contains(term)));
        }

        return await query.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<Department?> GetDepartmentByIdAsync(int id)
    {
        return await _context.Departments
            .Include(d => d.Events)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Department?> GetDepartmentByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var cleanCode = code.Trim().ToUpperInvariant();
        return await _context.Departments.FirstOrDefaultAsync(d => d.Code.ToUpper() == cleanCode);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, Department? Department)> CreateDepartmentAsync(string name, string code, string? description, string? headOfDepartment)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Department name is required.", null);

        if (string.IsNullOrWhiteSpace(code))
            return (false, "Department code is required.", null);

        var cleanCode = code.Trim().ToUpperInvariant();
        var cleanName = name.Trim();

        var codeExists = await _context.Departments.AnyAsync(d => d.Code.ToUpper() == cleanCode);
        if (codeExists)
            return (false, $"A department with code '{cleanCode}' already exists.", null);

        var dept = new Department
        {
            Name = cleanName,
            Code = cleanCode,
            Description = description?.Trim(),
            HeadOfDepartment = headOfDepartment?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Departments.Add(dept);
        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("CreateDepartment", nameof(Department), dept.Id.ToString(), null,
            newValues: $"Code={dept.Code}, Name={dept.Name}");

        _logger.LogInformation("Department created: {Code} - {Name}", dept.Code, dept.Name);
        return (true, null, dept);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, Department? Department)> UpdateDepartmentAsync(int id, string name, string code, string? description, string? headOfDepartment, bool isActive)
    {
        var dept = await _context.Departments.FindAsync(id);
        if (dept == null)
            return (false, "Department not found.", null);

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Department name is required.", null);

        if (string.IsNullOrWhiteSpace(code))
            return (false, "Department code is required.", null);

        var cleanCode = code.Trim().ToUpperInvariant();
        var cleanName = name.Trim();

        var codeConflict = await _context.Departments.AnyAsync(d => d.Id != id && d.Code.ToUpper() == cleanCode);
        if (codeConflict)
            return (false, $"Another department with code '{cleanCode}' already exists.", null);

        var oldValues = $"Name={dept.Name}, Code={dept.Code}, IsActive={dept.IsActive}";

        dept.Name = cleanName;
        dept.Code = cleanCode;
        dept.Description = description?.Trim();
        dept.HeadOfDepartment = headOfDepartment?.Trim();
        dept.IsActive = isActive;
        dept.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("UpdateDepartment", nameof(Department), dept.Id.ToString(), null,
            oldValues: oldValues,
            newValues: $"Name={dept.Name}, Code={dept.Code}, IsActive={dept.IsActive}");

        _logger.LogInformation("Department updated: ID {Id}, {Code} - {Name}", id, dept.Code, dept.Name);
        return (true, null, dept);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ToggleStatusAsync(int id)
    {
        var dept = await _context.Departments.FindAsync(id);
        if (dept == null)
            return (false, "Department not found.");

        dept.IsActive = !dept.IsActive;
        dept.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("ToggleDepartmentStatus", nameof(Department), dept.Id.ToString(), null,
            newValues: $"IsActive={dept.IsActive}");

        return (true, null);
    }

    public async Task<(bool CanDelete, string? Reason)> CanDeleteDepartmentAsync(int id)
    {
        var dept = await _context.Departments.FindAsync(id);
        if (dept == null)
            return (false, "Department not found.");

        var hasEvents = await _context.Events.IgnoreQueryFilters().AnyAsync(e => e.DepartmentId == id);
        if (hasEvents)
            return (false, $"Cannot delete department '{dept.Name}' because it has existing events associated with it. Please deactivate the department instead.");

        var hasUsers = await _context.Users.AnyAsync(u => u.Department == dept.Name);
        if (hasUsers)
            return (false, $"Cannot delete department '{dept.Name}' because registered users belong to this department. Please deactivate the department instead.");

        var hasEligibilities = await _context.EventEligibilities.IgnoreQueryFilters().AnyAsync(ee => ee.DepartmentId == id);
        if (hasEligibilities)
            return (false, $"Cannot delete department '{dept.Name}' because it is referenced in event eligibility rules.");

        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteDepartmentAsync(int id)
    {
        var (canDelete, reason) = await CanDeleteDepartmentAsync(id);
        if (!canDelete)
            return (false, reason);

        var dept = await _context.Departments.FindAsync(id);
        if (dept == null)
            return (false, "Department not found.");

        _context.Departments.Remove(dept);
        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("DeleteDepartment", nameof(Department), id.ToString(), null,
            oldValues: $"Name={dept.Name}, Code={dept.Code}");

        return (true, null);
    }
}


using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Core.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityName, string? entityId, string? userId, string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null);
    Task<IReadOnlyList<AuditLog>> GetRecentLogsAsync(int count = 50, string? entityName = null);
}


using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CollegeEventRegistration.Web.Services;

/// <summary>
/// Verifies operational connectivity to the primary SQL Server database instance.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(ApplicationDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("SQL Server database is online and reachable.");
            }

            _logger.LogWarning("Health check failed: SQL Server database reported CanConnectAsync = false.");
            return HealthCheckResult.Unhealthy("Unable to connect to the SQL Server database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check error: Exception during database connectivity check.");
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}

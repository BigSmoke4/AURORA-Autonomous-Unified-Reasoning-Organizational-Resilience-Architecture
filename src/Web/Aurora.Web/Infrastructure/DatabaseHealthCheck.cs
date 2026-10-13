using Aurora.Web.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aurora.Web.Infrastructure;

/// <summary>Readiness probe for PostgreSQL (spec section 69). Uses the application's own DbContext so the
/// check exercises the same connection string and retry policy as real requests.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AuroraDbContext _db;

    public DatabaseHealthCheck(AuroraDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL did not accept a connection.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", ex);
        }
    }
}

using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Aurora.Shared.Kernel;

namespace Aurora.Web.Models;

/// <summary>
/// Reads the tenant from the signed-in principal's organization claim. It reads only the claim
/// and never touches the database, so the query filter can be evaluated on every query without
/// extra round trips. Requests from anonymous users, and design-time tools, have no tenant. A background job
/// scope sets its tenant through <see cref="JobTenantScope"/>.
/// </summary>
public sealed class RequestTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JobTenantScope _jobScope;

    public RequestTenantContext(IHttpContextAccessor httpContextAccessor, JobTenantScope jobScope)
    {
        _httpContextAccessor = httpContextAccessor;
        _jobScope = jobScope;
    }

    public Guid? OrganizationId
    {
        get
        {
            // A background job runs without a request. The dispatcher sets its tenant on its own scope, and nothing
            // in a request can set it, so an HTTP caller can never read another organization through this path.
            if (_jobScope.OrganizationId is { } jobOrganization) return jobOrganization;

            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(AuroraClaims.OrganizationId);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}

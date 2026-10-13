using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Aurora.Shared.Kernel;

namespace Aurora.Web.Models;

/// <summary>
/// Reads the tenant from the signed-in principal's organization claim. It reads only the claim
/// and never touches the database, so the query filter can be evaluated on every query without
/// extra round trips. Requests from anonymous users, and design-time tools, have no tenant.
/// </summary>
public sealed class RequestTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestTenantContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public Guid? OrganizationId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(AuroraClaims.OrganizationId);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}

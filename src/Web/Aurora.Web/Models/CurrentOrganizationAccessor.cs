using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Application;
using Aurora.Modules.Organizations.Domain;

namespace Aurora.Web.Models;

/// <summary>
/// Resolves the tenant (organization) for the current request (spec section 42). The organization id
/// comes only from the authenticated principal's claim, never from a route, query or body value, and
/// there is deliberately no fallback to "some" organization: a user without a tenant gets no tenant data.
/// The result is cached per request so a page does not hit the database once per call site.
/// </summary>
public sealed class CurrentOrganizationAccessor
{
    private const string RequestCacheKey = "aurora.currentOrganization";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrganizationDirectory _organizationDirectory;

    public CurrentOrganizationAccessor(IHttpContextAccessor httpContextAccessor, IOrganizationDirectory organizationDirectory)
    {
        _httpContextAccessor = httpContextAccessor;
        _organizationDirectory = organizationDirectory;
    }

    public async Task<Organization?> GetCurrentAsync(CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return null;

        if (httpContext.Items.TryGetValue(RequestCacheKey, out var cached)) return cached as Organization;

        Organization? organization = null;
        if (Guid.TryParse(httpContext.User.FindFirstValue(AuroraClaims.OrganizationId), out var organizationId))
        {
            organization = await _organizationDirectory.GetByIdAsync(organizationId, ct);
        }

        httpContext.Items[RequestCacheKey] = organization;
        return organization;
    }

    /// <summary>The signed-in user's id, or null when the request is not authenticated.</summary>
    public Guid? GetCurrentUserId()
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

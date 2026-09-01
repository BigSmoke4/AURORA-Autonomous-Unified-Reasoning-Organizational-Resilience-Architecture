using System.Security.Claims;
using Aurora.Modules.Organizations.Application;
using Aurora.Modules.Organizations.Domain;

namespace Aurora.Web.Models;

/// <summary>
/// Resolves "which organization is the current user working in" for a request. AURORA is
/// multi-tenant (spec section 42); every controller uses this instead of trusting a client-
/// supplied organization id, so tenant isolation is enforced server-side, not by UI filtering.
/// </summary>
public sealed class CurrentOrganizationAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrganizationDirectory _organizationDirectory;

    public CurrentOrganizationAccessor(IHttpContextAccessor httpContextAccessor, IOrganizationDirectory organizationDirectory)
    {
        _httpContextAccessor = httpContextAccessor;
        _organizationDirectory = organizationDirectory;
    }

    public async Task<Organization?> GetCurrentAsync(CancellationToken ct = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var orgIdClaim = user?.FindFirstValue("aurora:organizationId");

        if (Guid.TryParse(orgIdClaim, out var orgId))
        {
            var org = await _organizationDirectory.GetByIdAsync(orgId, ct);
            if (org is not null) return org;
        }

        // Fallback for the seeded single-tenant demo: use the first active organization.
        var all = await _organizationDirectory.GetAllActiveAsync(ct);
        return all.FirstOrDefault();
    }
}

using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Web.Models;

/// <summary>Adds the user's home organization id as a claim on sign-in, so
/// CurrentOrganizationAccessor can resolve tenant context without a database round trip on
/// every request (spec section 42, tenant isolation enforced server-side).</summary>
public sealed class AuroraClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuroraClaimsTransformation(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.HasClaim(c => c.Type == "aurora:organizationId")) return principal;

        var user = await _userManager.GetUserAsync(principal);
        if (user?.OrganizationId is Guid orgId)
        {
            var identity = (ClaimsIdentity)principal.Identity;
            identity.AddClaim(new Claim("aurora:organizationId", orgId.ToString()));
        }

        return principal;
    }
}

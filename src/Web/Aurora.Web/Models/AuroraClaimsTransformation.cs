using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Web.Models;

/// <summary>
/// Adds the user's home organization as a claim so tenant resolution needs no extra query per request
/// (spec section 42). Permission claims are already materialized at sign-in by ASP.NET Core Identity
/// from each role's claims, so they are not re-queried here.
/// </summary>
public sealed class AuroraClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuroraClaimsTransformation(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.HasClaim(c => c.Type == AuroraClaims.OrganizationId)) return principal;

        var user = await _userManager.GetUserAsync(principal);
        if (user?.OrganizationId is Guid organizationId && user.IsActive)
        {
            ((ClaimsIdentity)principal.Identity).AddClaim(new Claim(AuroraClaims.OrganizationId, organizationId.ToString()));
        }

        return principal;
    }
}

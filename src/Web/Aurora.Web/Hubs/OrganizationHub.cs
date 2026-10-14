using Aurora.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Aurora.Web.Hubs;

/// <summary>
/// Pushes domain notifications (decision transitions, alerts) to every signed-in browser of one organization.
/// Each connection joins the group named after its own organization claim, so tenants never receive each other's events.
/// </summary>
[Authorize]
public sealed class OrganizationHub(IConfiguration configuration) : AuroraHub(configuration)
{
    private const string GroupPrefix = "org:";

    public static string GroupName(Guid organizationId) => GroupPrefix + organizationId.ToString("D");

    protected override async Task OnAuthorizedConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(AuroraClaims.OrganizationId), out var organizationId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organizationId));
        }
    }
}

using Aurora.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Aurora.Web.Hubs;

/// <summary>
/// Pushes domain notifications (decision transitions, alerts) to every signed-in browser of one organization.
/// Each connection joins the group named after its own organization claim, so tenants never receive each other's events.
/// </summary>
[Authorize]
public sealed class OrganizationHub : Hub
{
    private const string GroupPrefix = "org:";

    public static string GroupName(Guid organizationId) => GroupPrefix + organizationId.ToString("D");

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirstValue(AuroraClaims.OrganizationId), out var organizationId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organizationId));
        }

        await base.OnConnectedAsync();
    }
}

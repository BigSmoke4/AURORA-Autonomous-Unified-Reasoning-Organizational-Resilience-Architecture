using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Hubs;

/// <summary>
/// Real-time channel for simulation progress (spec section 39/61). A signed-in browser joins the
/// group for one scenario before it starts a run. The group name includes the caller's own
/// organization claim, so a user can only join groups in their own tenant.
/// </summary>
[Authorize]
public class SimulationHub : Hub
{
    public async Task JoinScenario(string scenarioId)
    {
        var organizationId = ReadGuidClaim(Context.User, AuroraClaims.OrganizationId);
        if (organizationId is null || !Guid.TryParse(scenarioId, out var parsedScenarioId))
        {
            throw new HubException("The scenario could not be joined.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(organizationId.Value, parsedScenarioId));
    }

    public async Task LeaveScenario(string scenarioId)
    {
        var organizationId = ReadGuidClaim(Context.User, AuroraClaims.OrganizationId);
        if (organizationId is null || !Guid.TryParse(scenarioId, out var parsedScenarioId))
        {
            throw new HubException("The scenario could not be left.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(organizationId.Value, parsedScenarioId));
    }

    public static string GroupName(Guid organizationId, Guid scenarioId) => $"sim-{organizationId:N}-{scenarioId:N}";

    private static Guid? ReadGuidClaim(ClaimsPrincipal? principal, string claimType) =>
        Guid.TryParse(principal?.FindFirstValue(claimType), out var value) ? value : null;
}

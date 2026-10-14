using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Hubs;

/// <summary>
/// Base class for every hub. A WebSocket upgrade carries the session cookie, and SignalR does not check the Origin
/// header, so any page that a signed-in browser visits could otherwise open a socket as that user. Each connection is
/// therefore refused unless its Origin is this application's own origin or is listed in Security:AllowedOrigins.
/// </summary>
public abstract class AuroraHub(IConfiguration configuration) : Hub
{
    public sealed override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var allowed = configuration.GetSection(HubOriginPolicy.AllowedOriginsSection).Get<string[]>() ?? [];
        if (http is null || !HubOriginPolicy.IsAllowed(http.Request.Headers[Microsoft.Net.Http.Headers.HeaderNames.Origin].ToString(),
                http.Request.Scheme, http.Request.Host, allowed))
        {
            Context.Abort();
            return;
        }

        await OnAuthorizedConnectedAsync();
        await base.OnConnectedAsync();
    }

    /// <summary>Runs once the origin has been accepted. Hubs override this to join their groups.</summary>
    protected virtual Task OnAuthorizedConnectedAsync() => Task.CompletedTask;
}

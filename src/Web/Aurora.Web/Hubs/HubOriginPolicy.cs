using Microsoft.AspNetCore.Http;

namespace Aurora.Web.Hubs;

/// <summary>Decides whether a hub connection comes from an origin this application serves.</summary>
public static class HubOriginPolicy
{
    public const string AllowedOriginsSection = "Security:AllowedOrigins";

    /// <summary>
    /// True when the origin is the application's own scheme and host, or is listed explicitly. A missing origin is
    /// refused: browsers always send one on a WebSocket upgrade.
    /// </summary>
    public static bool IsAllowed(string? origin, string requestScheme, HostString requestHost, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;

        var trimmed = origin.Trim().TrimEnd('/');
        if (allowedOrigins.Any(allowed => string.Equals(allowed.Trim().TrimEnd('/'), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;

        return string.Equals(uri.Scheme, requestScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Authority, requestHost.Value, StringComparison.OrdinalIgnoreCase);
    }
}

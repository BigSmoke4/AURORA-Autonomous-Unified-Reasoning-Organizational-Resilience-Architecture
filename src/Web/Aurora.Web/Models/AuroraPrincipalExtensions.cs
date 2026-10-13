using System.Security.Claims;

namespace Aurora.Web.Models;

public static class AuroraPrincipalExtensions
{
    public const string UnknownActor = "unknown";

    /// <summary>The name written to audit entries. It is never a credential or a secret.</summary>
    public static string ActorName(this ClaimsPrincipal principal) => principal.Identity?.Name ?? UnknownActor;
}

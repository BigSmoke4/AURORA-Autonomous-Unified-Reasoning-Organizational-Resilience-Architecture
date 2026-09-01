using Microsoft.AspNetCore.Identity;

namespace Aurora.Modules.Identity.Domain;

/// <summary>
/// AURORA's identity principal. Extends ASP.NET Core Identity's IdentityUser with the
/// tenant + display context every module needs when attributing actions to a user
/// (audit log, decision approvals, AI investigation ownership).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>The organization (tenant) this user primarily belongs to. Nullable for
    /// system administrators who operate across tenants.</summary>
    public Guid? OrganizationId { get; set; }

    public string? JobTitle { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}

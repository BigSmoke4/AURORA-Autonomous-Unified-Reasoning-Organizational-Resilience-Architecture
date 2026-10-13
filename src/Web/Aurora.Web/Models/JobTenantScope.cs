namespace Aurora.Web.Models;

/// <summary>
/// The tenant for a background job's service scope. Only the job dispatcher writes it, and it lives in that scope alone,
/// so it never reaches an HTTP request.
/// </summary>
public sealed class JobTenantScope
{
    public Guid? OrganizationId { get; set; }
}

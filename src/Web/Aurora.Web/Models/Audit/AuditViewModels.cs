using Aurora.Modules.Audit.Domain;

namespace Aurora.Web.Models.Audit;

public sealed class AuditIndexViewModel
{
    public IReadOnlyList<AuditEntry> Entries { get; init; } = Array.Empty<AuditEntry>();
}

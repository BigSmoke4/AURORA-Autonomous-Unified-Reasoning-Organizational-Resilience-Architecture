using System.Diagnostics;
using Aurora.Modules.Audit.Contracts;
using Aurora.Modules.Audit.Domain;

namespace Aurora.Modules.Audit.Application;

/// <summary>
/// Stages audit entries on the caller's unit of work. It does not save: the caller's SaveChanges
/// commits the audit row together with the change it records (spec section 45).
/// </summary>
public sealed class AuditRecorder
{
    public const int MaxDetailsLength = 2000;

    private readonly IAuditRepository _repository;

    public AuditRecorder(IAuditRepository repository) => _repository = repository;

    public void Record(Guid? organizationId, Guid? actorUserId, string actorName, string action,
        string entityType, string? entityId, AuditOutcome outcome, string? details = null)
    {
        var trimmed = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details;
        var correlationId = Activity.Current?.TraceId.ToString();
        _repository.Add(new AuditEntry(organizationId, actorUserId, actorName, action, entityType,
            entityId, outcome, trimmed, correlationId));
    }
}

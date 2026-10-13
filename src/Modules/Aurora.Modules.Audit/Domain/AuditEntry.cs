using Aurora.Shared.Kernel;

namespace Aurora.Modules.Audit.Domain;

public enum AuditOutcome { Success = 0, Failure = 1, Denied = 2 }

/// <summary>
/// An append-only record of a security-relevant or decision-relevant action (spec section 45).
/// Entries are written in the same SaveChanges call as the action they describe, so an action
/// and its audit record commit or roll back together. Sign-in events may have no organization.
/// </summary>
public class AuditEntry : Entity
{
    public Guid? OrganizationId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public AuditOutcome Outcome { get; private set; }
    public string? Details { get; private set; }
    public string? CorrelationId { get; private set; }

    protected AuditEntry() { }

    public AuditEntry(Guid? organizationId, Guid? actorUserId, string actorName, string action,
        string entityType, string? entityId, AuditOutcome outcome, string? details, string? correlationId)
    {
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        ActorName = actorName;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Outcome = outcome;
        Details = details;
        CorrelationId = correlationId;
    }
}

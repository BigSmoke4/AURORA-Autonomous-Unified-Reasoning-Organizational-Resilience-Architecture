using Aurora.Shared.Kernel;

namespace Aurora.Modules.Alerts.Domain;

public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3,
}

public enum AlertStatus
{
    Open = 1,
    Acknowledged = 2,
    Resolved = 3,
}

/// <summary>
/// An operational alert with a strict lifecycle: Open to Acknowledged to Resolved, or Open to Resolved.
/// Acknowledging is only allowed from Open, and resolving is not allowed once resolved (spec section 28).
/// </summary>
public class Alert : AggregateRoot, ITenantScoped
{
    public const int SourceKeyMaxLength = 200;
    public const int TitleMaxLength = 300;
    public const int DetailMaxLength = 1000;

    public Guid OrganizationId { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Detail { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertStatus Status { get; private set; }
    public DateTime RaisedAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    protected Alert()
    {
    }

    public Alert(Guid organizationId, string sourceKey, string title, string? detail, AlertSeverity severity, DateTime raisedAtUtc)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        SourceKey = DomainGuard.RequireText(sourceKey, SourceKeyMaxLength, nameof(sourceKey));
        Title = DomainGuard.RequireText(title, TitleMaxLength, nameof(title));
        Detail = DomainGuard.OptionalText(detail, DetailMaxLength, nameof(detail));
        Severity = severity;
        Status = AlertStatus.Open;
        RaisedAtUtc = DomainGuard.ToUtc(raisedAtUtc);
        RaiseDomainEvent(new AlertRaised(OrganizationId, Id, Title, Severity, RaisedAtUtc));
    }

    public void Acknowledge(Guid userId, DateTime nowUtc)
    {
        DomainGuard.RequireId(userId, nameof(userId));
        if (Status != AlertStatus.Open) throw new InvalidOperationException("Only an open alert can be acknowledged.");

        Status = AlertStatus.Acknowledged;
        AcknowledgedByUserId = userId;
        AcknowledgedAtUtc = DomainGuard.ToUtc(nowUtc);
        Touch();
    }

    public void Resolve(DateTime nowUtc)
    {
        if (Status == AlertStatus.Resolved) throw new InvalidOperationException("The alert is already resolved.");

        Status = AlertStatus.Resolved;
        ResolvedAtUtc = DomainGuard.ToUtc(nowUtc);
        Touch();
        RaiseDomainEvent(new AlertResolved(OrganizationId, Id, ResolvedAtUtc.Value));
    }
}

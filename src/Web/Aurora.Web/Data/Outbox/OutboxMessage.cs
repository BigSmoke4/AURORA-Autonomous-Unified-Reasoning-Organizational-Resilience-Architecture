using Aurora.Shared.Kernel;

namespace Aurora.Web.Data.Outbox;

/// <summary>
/// A domain event waiting to be dispatched. It is written in the same transaction as the change that raised it,
/// so an event exists if and only if its change was saved. The dispatcher marks it processed after its handlers run.
/// Delivery is at least once: a handler may see the same event again after a failed batch, so handlers must be idempotent.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public const int EventTypeMaxLength = 200;
    public const int PayloadMaxLength = 8000;
    public const int ErrorMaxLength = 200;

    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid organizationId, string eventType, string payload, DateTime occurredOnUtc)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        EventType = DomainGuard.RequireText(eventType, EventTypeMaxLength, nameof(eventType));
        Payload = DomainGuard.RequireText(payload, PayloadMaxLength, nameof(payload));
        OccurredOnUtc = DomainGuard.ToUtc(occurredOnUtc);
    }

    /// <summary>The tenant the event belongs to. The outbox itself is read by the platform, not by a tenant.</summary>
    public Guid OrganizationId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>Number of dispatch attempts. A message stops being retried once it reaches the configured maximum.</summary>
    public int Attempts { get; private set; }

    /// <summary>The exception type of the last failed attempt. Stack traces are never stored here.</summary>
    public string? LastError { get; private set; }

    public void MarkProcessed(DateTime nowUtc)
    {
        Attempts++;
        ProcessedOnUtc = DomainGuard.ToUtc(nowUtc);
        LastError = null;
        Touch();
    }

    public void RecordFailure(string errorType)
    {
        Attempts++;
        LastError = DomainGuard.OptionalText(errorType, ErrorMaxLength, nameof(errorType));
        Touch();
    }
}

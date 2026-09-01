namespace Aurora.Shared.Events;

/// <summary>
/// Marker for internal domain events. AURORA is a modular monolith (ADR 001), so events are
/// dispatched in-process via a lightweight custom dispatcher rather than a message broker —
/// this keeps module coupling loose without pretending to be a distributed system.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

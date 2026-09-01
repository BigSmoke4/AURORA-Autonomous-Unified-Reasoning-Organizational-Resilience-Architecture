using Aurora.Shared.Events;

namespace Aurora.Shared.Kernel;

/// <summary>
/// An aggregate root can raise domain events. Events are collected on the entity and
/// dispatched by the application layer (see Shared.Events.IDomainEventDispatcher) after
/// the unit of work commits successfully — never before, so a failed transaction never
/// leaks an event for something that didn't actually happen.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

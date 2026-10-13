using System.ComponentModel.DataAnnotations.Schema;

namespace Aurora.Shared.Kernel;

/// <summary>
/// Marks an entity as an aggregate root: the unit that transactions and tenant checks are applied to.
/// An aggregate records what happened as domain events. AuroraDbContext writes them to the transactional
/// outbox in the same SaveChanges call as the change, so an event exists if and only if its change is saved.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Events raised since the aggregate was last saved. Not persisted as a column.</summary>
    [NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by the persistence layer once the events have been staged for the outbox.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}

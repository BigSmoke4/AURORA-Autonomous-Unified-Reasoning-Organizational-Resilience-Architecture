namespace Aurora.Shared.Events;

/// <summary>Implemented by modules that react to a domain event raised by another module.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

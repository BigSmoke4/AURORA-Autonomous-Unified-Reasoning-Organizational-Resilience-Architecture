using Aurora.Shared.Kernel;

namespace Aurora.Web.Infrastructure.Events;

/// <summary>
/// Reacts to a domain event after it has been committed to the outbox. Handlers receive every event and ignore
/// the ones they do not care about. A handler that throws keeps the message for a later retry, so it must be idempotent.
/// </summary>
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}

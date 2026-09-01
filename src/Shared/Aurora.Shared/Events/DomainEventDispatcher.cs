using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aurora.Shared.Events;

/// <summary>
/// Resolves and invokes every registered IDomainEventHandler&lt;T&gt; for each event.
/// Called after the unit of work commits (see AuroraDbContext.SaveChangesAsync), so a handler
/// only ever sees events for state changes that actually persisted.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            var eventType = domainEvent.GetType();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handlers = _serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler is null) continue;
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                try
                {
                    if (method?.Invoke(handler, new object[] { domainEvent, cancellationToken }) is Task task)
                        await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler {Handler} failed for event {Event} ({EventId})",
                        handler.GetType().Name, eventType.Name, domainEvent.EventId);
                }
            }
        }
    }
}

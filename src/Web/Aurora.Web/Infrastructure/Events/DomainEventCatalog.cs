using System.Text.Json;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Shared.Kernel;

namespace Aurora.Web.Infrastructure.Events;

/// <summary>
/// The closed set of domain events that may enter the outbox. An event type must be listed here to be staged, so the
/// dispatcher can always deserialize what was written. Names are stored in the outbox, so renaming a type is a data change.
/// </summary>
public static class DomainEventCatalog
{
    private static readonly IReadOnlyDictionary<string, Type> TypesByName = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [nameof(DecisionStateChanged)] = typeof(DecisionStateChanged),
        [nameof(AlertRaised)] = typeof(AlertRaised),
        [nameof(AlertResolved)] = typeof(AlertResolved),
    };

    public static string NameOf(IDomainEvent domainEvent)
    {
        var name = domainEvent.GetType().Name;
        return TypesByName.ContainsKey(name)
            ? name
            : throw new InvalidOperationException($"Domain event '{name}' is not registered in the outbox catalog.");
    }

    public static string Serialize(IDomainEvent domainEvent) => JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

    public static IDomainEvent Deserialize(string name, string payload)
    {
        if (!TypesByName.TryGetValue(name, out var type))
        {
            throw new InvalidOperationException($"Domain event '{name}' is not registered in the outbox catalog.");
        }

        return JsonSerializer.Deserialize(payload, type) as IDomainEvent
            ?? throw new InvalidOperationException($"Domain event '{name}' could not be read from the outbox.");
    }
}

using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Shared.Kernel;
using Aurora.Web.Data.Outbox;
using Aurora.Web.Infrastructure.Events;
using Xunit;

namespace Aurora.Tests.Events;

public class DomainEventCatalogTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly DateTime OccurredAt = new(2026, 10, 13, 9, 30, 15, 250, DateTimeKind.Utc);

    public static TheoryData<IDomainEvent> RegisteredEvents => new()
    {
        new DecisionStateChanged(OrganizationId, Guid.NewGuid(), DecisionChanges.Approved, OccurredAt),
        new AlertRaised(OrganizationId, Guid.NewGuid(), "Supplier concentration high", AlertSeverity.Critical, OccurredAt),
        new AlertResolved(OrganizationId, Guid.NewGuid(), OccurredAt),
    };

    [Theory]
    [MemberData(nameof(RegisteredEvents))]
    public void RegisteredEvent_RoundTripsThroughThePayload(IDomainEvent original)
    {
        var name = DomainEventCatalog.NameOf(original);
        var payload = DomainEventCatalog.Serialize(original);

        var restored = DomainEventCatalog.Deserialize(name, payload);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void UnregisteredEvent_IsRejectedWhenNamed()
    {
        var unknown = new UnregisteredEvent(OrganizationId, OccurredAt);

        Assert.Throws<InvalidOperationException>(() => DomainEventCatalog.NameOf(unknown));
    }

    [Fact]
    public void UnknownName_IsRejectedWhenDeserialized()
    {
        Assert.Throws<InvalidOperationException>(() => DomainEventCatalog.Deserialize("NoSuchEvent", "{}"));
    }

    [Fact]
    public void Payload_AtTheLimit_IsAccepted()
    {
        var payload = new string('x', OutboxMessage.PayloadMaxLength);

        var message = new OutboxMessage(OrganizationId, nameof(AlertResolved), payload, OccurredAt);

        Assert.Equal(OutboxMessage.PayloadMaxLength, message.Payload.Length);
    }

    [Fact]
    public void Payload_OverTheLimit_IsRejected()
    {
        var payload = new string('x', OutboxMessage.PayloadMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new OutboxMessage(OrganizationId, nameof(AlertResolved), payload, OccurredAt));
    }

    [Fact]
    public void Failure_IsCountedAndKeepsOnlyTheErrorType()
    {
        var message = new OutboxMessage(OrganizationId, nameof(AlertResolved), "{}", OccurredAt);

        message.RecordFailure(nameof(InvalidOperationException));

        Assert.Equal(1, message.Attempts);
        Assert.Equal(nameof(InvalidOperationException), message.LastError);
        Assert.Null(message.ProcessedOnUtc);
    }

    [Fact]
    public void Success_AfterFailure_ClearsTheError()
    {
        var message = new OutboxMessage(OrganizationId, nameof(AlertResolved), "{}", OccurredAt);
        message.RecordFailure(nameof(InvalidOperationException));

        message.MarkProcessed(OccurredAt.AddSeconds(5));

        Assert.Equal(2, message.Attempts);
        Assert.Null(message.LastError);
        Assert.NotNull(message.ProcessedOnUtc);
    }

    private sealed record UnregisteredEvent(Guid OrganizationId, DateTime OccurredOnUtc) : IDomainEvent;
}

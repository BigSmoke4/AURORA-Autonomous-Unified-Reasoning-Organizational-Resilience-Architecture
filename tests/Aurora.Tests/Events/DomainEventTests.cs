using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Decisions.Domain;
using Xunit;

namespace Aurora.Tests.Events;

/// <summary>Aggregates raise events only for transitions that actually happened.</summary>
public class DomainEventTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid Proposer = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly DateTime RaisedAt = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    private static Decision NewDecision() => new(new DecisionDraft(
        OrganizationId,
        "Dual-source the polymer supplier",
        "Qualify a second supplier",
        "Single-source exposure above threshold",
        72.5m,
        80m,
        DecisionSourceKind.Manual,
        null,
        Proposer,
        "test-model-v1",
        250_000m,
        "{}"));

    [Fact]
    public void Approve_RaisesApprovedEvent()
    {
        var decision = NewDecision();

        decision.Approve(Approver);

        var domainEvent = Assert.Single(decision.DomainEvents);
        var changed = Assert.IsType<DecisionStateChanged>(domainEvent);
        Assert.Equal(DecisionChanges.Approved, changed.Change);
        Assert.Equal(decision.Id, changed.DecisionId);
        Assert.Equal(OrganizationId, changed.OrganizationId);
    }

    [Fact]
    public void Reject_RaisesRejectedEvent()
    {
        var decision = NewDecision();

        decision.Reject(Approver);

        var changed = Assert.IsType<DecisionStateChanged>(Assert.Single(decision.DomainEvents));
        Assert.Equal(DecisionChanges.Rejected, changed.Change);
    }

    [Fact]
    public void ExecutionAndOutcome_RaiseEventsInOrder()
    {
        var decision = NewDecision();
        decision.Approve(Approver);
        decision.ClearDomainEvents();

        decision.StartExecution();
        decision.RecordActualOutcome(240_000m, "Second supplier qualified; revenue held.");

        var changes = decision.DomainEvents.Cast<DecisionStateChanged>().Select(e => e.Change).ToArray();
        Assert.Equal(new[] { DecisionChanges.ExecutionStarted, DecisionChanges.OutcomeRecorded }, changes);
    }

    [Fact]
    public void RejectedTransition_RaisesNoEvent()
    {
        var decision = NewDecision();

        Assert.Throws<InvalidOperationException>(() => decision.Approve(Proposer));

        Assert.Empty(decision.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheBuffer()
    {
        var decision = NewDecision();
        decision.Approve(Approver);

        decision.ClearDomainEvents();

        Assert.Empty(decision.DomainEvents);
    }

    [Fact]
    public void Alert_RaisesRaisedThenResolvedEvents()
    {
        var alert = new Alert(OrganizationId, "monitoring:metric:1", "Yield below minimum", "Yield 91%", AlertSeverity.Warning, RaisedAt);

        var raised = Assert.IsType<AlertRaised>(Assert.Single(alert.DomainEvents));
        Assert.Equal(alert.Id, raised.AlertId);
        Assert.Equal(AlertSeverity.Warning, raised.Severity);
        Assert.Equal("Yield below minimum", raised.Title);
        alert.ClearDomainEvents();

        alert.Resolve(RaisedAt.AddHours(1));

        var resolved = Assert.IsType<AlertResolved>(Assert.Single(alert.DomainEvents));
        Assert.Equal(alert.Id, resolved.AlertId);
        Assert.Equal(RaisedAt.AddHours(1), resolved.OccurredOnUtc);
    }
}

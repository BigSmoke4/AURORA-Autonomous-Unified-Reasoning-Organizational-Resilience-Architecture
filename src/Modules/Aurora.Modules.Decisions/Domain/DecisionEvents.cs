using Aurora.Shared.Kernel;

namespace Aurora.Modules.Decisions.Domain;

/// <summary>Raised when a decision moves through approval, execution or outcome recording.</summary>
public sealed record DecisionStateChanged(
    Guid OrganizationId,
    Guid DecisionId,
    string Change,
    DateTime OccurredOnUtc) : IDomainEvent;

/// <summary>The values of <see cref="DecisionStateChanged.Change"/>. Stored in the outbox as text.</summary>
public static class DecisionChanges
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ExecutionStarted = "ExecutionStarted";
    public const string OutcomeRecorded = "OutcomeRecorded";
}

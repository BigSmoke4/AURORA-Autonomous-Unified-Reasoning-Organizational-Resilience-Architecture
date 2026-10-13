namespace Aurora.Shared.Kernel;

/// <summary>
/// A fact that happened inside a module, raised by an aggregate. Events are immutable records. Each names its tenant
/// so the outbox dispatcher can route notifications to the right organization.
/// </summary>
public interface IDomainEvent
{
    Guid OrganizationId { get; }

    DateTime OccurredOnUtc { get; }
}

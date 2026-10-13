namespace Aurora.Shared.Kernel;

/// <summary>
/// Marks an entity as an aggregate root: the unit that transactions and tenant checks are applied to.
/// Domain events are not implemented yet; see docs/ARCHITECTURE.md.
/// </summary>
public abstract class AggregateRoot : Entity
{
}

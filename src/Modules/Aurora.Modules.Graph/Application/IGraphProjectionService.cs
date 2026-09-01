namespace Aurora.Modules.Graph.Application;

/// <summary>Spec section 9. Builds graph snapshots for the four enterprise states
/// (spec section 10): Live, Historical, Scenario, Simulation. Only Live is implemented against
/// real persisted data today; Historical/Scenario/Simulation snapshots are built by cloning the
/// live snapshot and applying deltas (see ScenarioGraphSnapshotFactory) rather than a fabricated
/// second data source.</summary>
public interface IGraphProjectionService
{
    Task<GraphSnapshot> BuildLiveGraphAsync(Guid organizationId, CancellationToken ct = default);
}

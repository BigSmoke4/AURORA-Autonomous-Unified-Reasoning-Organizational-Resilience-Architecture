using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Application;
using Xunit;

namespace Aurora.Tests;

public class SimulationEngineTests
{
    private readonly Guid _org = Guid.NewGuid();

    [Fact]
    public void Run_ProducesZeroImpact_OnDayZero_ForTargetAtFullSeverity()
    {
        var target = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "Target", 3, 10_000m);
        var snapshot = new GraphSnapshot(_org, new[] { target }, Array.Empty<GraphEdge>());

        var scenario = new Scenario(_org, "Test Scenario", target.Id, ScenarioEventType.FullShutdown,
            severity: 1.0m, durationDays: 10, startDateUtc: DateTime.UtcNow, createdByUserId: Guid.NewGuid());

        var engine = new SimulationEngine(new GraphTraversalService());
        var output = engine.Run(Guid.NewGuid(), scenario, snapshot);

        var day0 = output.Steps.FirstOrDefault(s => s.NodeId == target.Id && s.DayNumber == 0);
        // With a 10-day duration, ramp-in is min(5, 10) = 5 days, so day 0 impact should be 0
        // and therefore not recorded (the engine skips zero-impact steps).
        Assert.Null(day0);

        var day5 = output.Steps.First(s => s.NodeId == target.Id && s.DayNumber == 5);
        Assert.Equal(1.0m, day5.ImpactScore); // ramp-in complete, full severity holds
    }

    [Fact]
    public void Run_PropagatesImpact_ToDownstreamNodes_AttenuatedByEdgeStrength()
    {
        var supplier = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "Supplier", 5, 0m);
        var factory = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Factory", 4, 50_000m);

        // Factory DEPENDS on Supplier: edge Supplier->Factory represents "Supplier supplies Factory".
        // Simulation propagates via DownstreamImpact, which walks INCOMING edges of the target —
        // so an edge Supplier -> Factory means Factory is downstream of Supplier.
        var edge = new GraphEdge(_org, supplier.Id, factory.Id, GraphRelationshipType.Supplies, strength: 0.5m, criticality: 4);
        var snapshot = new GraphSnapshot(_org, new[] { supplier, factory }, new[] { edge });

        var scenario = new Scenario(_org, "Supplier failure", supplier.Id, ScenarioEventType.FullShutdown,
            severity: 1.0m, durationDays: 30, startDateUtc: DateTime.UtcNow, createdByUserId: Guid.NewGuid());

        var engine = new SimulationEngine(new GraphTraversalService());
        var output = engine.Run(Guid.NewGuid(), scenario, snapshot);

        var factorySteadyStateStep = output.Steps
            .Where(s => s.NodeId == factory.Id && s.DayNumber >= 5 && s.DayNumber <= scenario.DurationDays)
            .OrderByDescending(s => s.DayNumber)
            .First();

        // Impact on factory = severity(1.0) * propagatedStrength(0.5) = 0.5, attenuated by the
        // edge strength — never equal to or exceeding the source's own impact.
        Assert.Equal(0.5m, factorySteadyStateStep.ImpactScore);
        Assert.True(output.Result.TotalRevenueImpact > 0);
    }

    [Fact]
    public void Run_HigherCriticality_ProducesLongerRecoveryWindow()
    {
        var lowCriticality = new GraphNode(_org, GraphNodeType.Warehouse, Guid.NewGuid(), "LowCrit", 1, 1000m);
        var highCriticality = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "HighCrit", 5, 1000m);

        var snapshotLow = new GraphSnapshot(_org, new[] { lowCriticality }, Array.Empty<GraphEdge>());
        var snapshotHigh = new GraphSnapshot(_org, new[] { highCriticality }, Array.Empty<GraphEdge>());

        var scenarioLow = new Scenario(_org, "Low", lowCriticality.Id, ScenarioEventType.FullShutdown, 1m, 10, DateTime.UtcNow, Guid.NewGuid());
        var scenarioHigh = new Scenario(_org, "High", highCriticality.Id, ScenarioEventType.FullShutdown, 1m, 10, DateTime.UtcNow, Guid.NewGuid());

        var engine = new SimulationEngine(new GraphTraversalService());
        var outputLow = engine.Run(Guid.NewGuid(), scenarioLow, snapshotLow);
        var outputHigh = engine.Run(Guid.NewGuid(), scenarioHigh, snapshotHigh);

        Assert.True(outputHigh.Result.RecoveryTimeDays > outputLow.Result.RecoveryTimeDays);
    }
}

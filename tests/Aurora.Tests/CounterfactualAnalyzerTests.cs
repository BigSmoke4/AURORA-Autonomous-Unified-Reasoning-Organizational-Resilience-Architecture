using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Application;
using Xunit;

namespace Aurora.Tests;

public class CounterfactualAnalyzerTests
{
    private readonly Guid _org = Guid.NewGuid();

    private (GraphSnapshot Snapshot, Scenario Scenario) BuildSingleSourceGraph()
    {
        var supplier = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "Sole supplier", 5, 0m);
        var factory = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Factory", 4, 500_000m);
        var customer = new GraphNode(_org, GraphNodeType.Customer, Guid.NewGuid(), "Customer", 3, 250_000m);
        var edges = new[]
        {
            new GraphEdge(_org, supplier.Id, factory.Id, GraphRelationshipType.Supplies, 0.9m, 5),
            new GraphEdge(_org, factory.Id, customer.Id, GraphRelationshipType.Produces, 0.8m, 4),
        };
        var snapshot = new GraphSnapshot(_org, new[] { supplier, factory, customer }, edges);
        var scenario = new Scenario(_org, "Supplier outage", supplier.Id, ScenarioEventType.FullShutdown,
            severity: 1.0m, durationDays: 30, startDateUtc: DateTime.UtcNow, createdByUserId: Guid.NewGuid());
        return (snapshot, scenario);
    }

    private static CounterfactualAnalyzer CreateAnalyzer() =>
        new(new SimulationEngine(new GraphTraversalService()));

    [Fact]
    public void ZeroMitigationShare_ReproducesTheActualRunExactly()
    {
        var (snapshot, scenario) = BuildSingleSourceGraph();

        var result = CreateAnalyzer().Analyze(scenario, snapshot, 0m);

        Assert.Equal(0m, result.MitigationShare);
        Assert.Equal(0m, result.RevenueImpactAvoided);
        Assert.Equal(0m, result.PeakImpactReductionPct);
        Assert.Equal(result.Actual, result.Counterfactual);
    }

    [Fact]
    public void RevenueImpactAvoided_IsNonDecreasingInMitigationShare()
    {
        var (snapshot, scenario) = BuildSingleSourceGraph();
        var analyzer = CreateAnalyzer();

        var shares = new[] { 0m, 0.25m, 0.5m, 0.75m, 1m };
        var avoided = shares.Select(share => analyzer.Analyze(scenario, snapshot, share).RevenueImpactAvoided).ToArray();

        for (var i = 1; i < avoided.Length; i++)
        {
            Assert.True(avoided[i] >= avoided[i - 1],
                $"Avoided impact fell from {avoided[i - 1]} to {avoided[i]} when the share rose to {shares[i]}.");
        }
        Assert.True(avoided[^1] > 0m, "Full mitigation of a propagating dependency should avoid some impact.");
    }

    [Fact]
    public void Analyze_IsDeterministic_ForEqualInputs()
    {
        var (snapshot, scenario) = BuildSingleSourceGraph();
        var analyzer = CreateAnalyzer();

        var first = analyzer.Analyze(scenario, snapshot, 0.5m);
        var second = analyzer.Analyze(scenario, snapshot, 0.5m);

        Assert.Equal(first, second);
        Assert.Equal(CounterfactualAnalyzer.Method, first.Method);
    }

    [Fact]
    public void Analyze_RejectsMitigationShareOutsideZeroToOne()
    {
        var (snapshot, scenario) = BuildSingleSourceGraph();

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAnalyzer().Analyze(scenario, snapshot, 1.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAnalyzer().Analyze(scenario, snapshot, -0.01m));
    }
}

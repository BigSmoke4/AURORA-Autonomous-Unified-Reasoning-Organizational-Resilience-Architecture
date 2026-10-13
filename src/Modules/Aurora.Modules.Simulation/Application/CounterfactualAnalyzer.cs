using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Modules.Simulation.Application;

/// <summary>Headline numbers from one simulation, as stored inside a counterfactual comparison.</summary>
public sealed record SimulationSummary(
    decimal TotalRevenueImpact,
    decimal PeakDailyImpactScore,
    int AffectedNodeCount,
    int RecoveryTimeDays);

/// <summary>
/// Actual-versus-counterfactual comparison. Both sides are produced by the same deterministic
/// SimulationEngine; the only difference is the mitigation applied to the graph.
/// </summary>
public sealed record CounterfactualComparison(
    string ModelVersion,
    string Method,
    Guid ScenarioId,
    string TargetName,
    decimal MitigationShare,
    SimulationSummary Actual,
    SimulationSummary Counterfactual,
    decimal RevenueImpactAvoided,
    decimal PeakImpactReductionPct);

/// <summary>
/// Counterfactual analysis (spec section 14). Models a diversification of the scenario target's
/// supply: a share of the target's downstream dependence is moved to alternate sources, so every
/// outgoing edge of the target is attenuated by (1 - share). The engine then re-runs the same
/// scenario on the mitigated graph.
///
/// Limits (also stated in ADR 005 and the UI): this is a single-step, graph-level what-if. It does
/// not replay historical state, does not model the cost of the mitigation, and does not model
/// lead time for alternate sources.
/// </summary>
public sealed class CounterfactualAnalyzer
{
    public const string Method = "target-edge-attenuation-v1";
    public const int PercentScale = 100;
    private const int PercentDecimals = 2;
    private const int CurrencyDecimals = 2;

    private readonly SimulationEngine _engine;

    public CounterfactualAnalyzer(SimulationEngine engine) => _engine = engine;

    public CounterfactualComparison Analyze(Scenario scenario, GraphSnapshot snapshot, decimal mitigationShare)
    {
        if (mitigationShare is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(mitigationShare), "Mitigation share must be between 0 and 1.");

        var target = snapshot.Find(scenario.TargetNodeId)
            ?? throw new InvalidOperationException("Scenario target is not present in the graph snapshot.");

        var actual = _engine.Run(Guid.Empty, scenario, snapshot).Result;
        var mitigated = Attenuate(snapshot, scenario.TargetNodeId, 1m - mitigationShare);
        var counterfactual = _engine.Run(Guid.Empty, scenario, mitigated).Result;

        var actualSummary = new SimulationSummary(actual.TotalRevenueImpact, actual.PeakDailyImpactScore,
            actual.AffectedNodeCount, actual.RecoveryTimeDays);
        var counterfactualSummary = new SimulationSummary(counterfactual.TotalRevenueImpact,
            counterfactual.PeakDailyImpactScore, counterfactual.AffectedNodeCount, counterfactual.RecoveryTimeDays);

        var avoided = Math.Round(actual.TotalRevenueImpact - counterfactual.TotalRevenueImpact, CurrencyDecimals);
        var peakReduction = actual.PeakDailyImpactScore == 0m
            ? 0m
            : Math.Round((actual.PeakDailyImpactScore - counterfactual.PeakDailyImpactScore)
                / actual.PeakDailyImpactScore * PercentScale, PercentDecimals);

        return new CounterfactualComparison(
            SimulationEngine.ModelVersion, Method, scenario.Id, target.DisplayName, mitigationShare,
            actualSummary, counterfactualSummary, avoided, peakReduction);
    }

    private static GraphSnapshot Attenuate(GraphSnapshot snapshot, Guid targetNodeId, decimal retainedShare)
    {
        var edges = snapshot.Edges.Select(edge => edge.SourceNodeId == targetNodeId
            ? new GraphEdge(edge.OrganizationId, edge.SourceNodeId, edge.TargetNodeId, edge.RelationshipType,
                edge.Strength * retainedShare, edge.Criticality, edge.Metadata)
            : edge);
        return new GraphSnapshot(snapshot.OrganizationId, snapshot.Nodes.Values, edges);
    }
}

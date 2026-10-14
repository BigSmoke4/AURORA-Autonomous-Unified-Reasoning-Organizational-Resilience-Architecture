using Aurora.Modules.Graph.Application;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Domain;

namespace Aurora.Modules.Simulation.Application;

public sealed record SimulationOutput(
    IReadOnlyList<SimulationStep> Steps,
    SimulationResult Result);

/// <summary>
/// The deterministic Simulation Engine (spec section 13, 74, 100). Every number this class
/// produces is computed from the graph snapshot's Strength/Criticality/RevenueExposure fields
/// and the scenario's Severity/Duration — nothing here is generated or estimated by an AI model.
/// The AI layer (Modules.AI) may only read and explain these outputs; it never overrides them.
///
/// Model (documented so the math is auditable, per spec section 76 "model versioning"):
///   1. Day 0: the target node's impact = scenario.Severity.
///   2. Impact propagates outward along the DOWNSTREAM (incoming-edge / "who depends on me")
///      direction of the graph, using GraphTraversalService.DownstreamImpact so the propagation
///      logic is shared with the interactive "what would be affected if X failed" graph queries.
///   3. Each hop attenuates by the traversed edge's Strength (already computed by DownstreamImpact
///      as PropagatedStrength = product of edge strengths along the path).
///   4. Ramp-in: impact rises linearly from 0 to full propagated strength over the first
///      min(5, DurationDays) days, holds steady until DurationDays, then recovers linearly over
///      a recovery window proportional to the node's criticality (higher criticality => slower
///      recovery, reflecting that critical nodes take longer to restore to full function).
///   5. Daily revenue impact for a node = node.RevenueExposure / 365 * day's impact score.
///      Total revenue impact = sum over all affected nodes and all simulated days.
/// </summary>
public sealed class SimulationEngine
{
    /// <summary>Recorded with every analysis so results remain reproducible across model changes.</summary>
    public const string ModelVersion = "aurora-sim-v1";

    private const int DaysPerYear = 365;
    private const int MaxPropagationHops = 10;
    private const int MaxRampInDays = 5;
    private const int MinimumRecoveryWindowDays = 5;
    private const int RecoveryDaysPerCriticalityPoint = 10;
    private const decimal CriticalImpactThreshold = 0.5m;
    private const decimal WarningImpactThreshold = 0.2m;
    private const decimal FullStrength = 1.0m;
    private const int ImpactScoreDecimals = 4;
    private const int CurrencyDecimals = 2;

    private readonly GraphTraversalService _traversal;

    public SimulationEngine(GraphTraversalService traversal) => _traversal = traversal;

    public SimulationOutput Run(Guid simulationRunId, Scenario scenario, GraphSnapshot snapshot)
    {
        var targetNode = snapshot.Find(scenario.TargetNodeId)
            ?? throw new InvalidOperationException($"Scenario target node {scenario.TargetNodeId} not found in graph snapshot.");

        var impacted = _traversal.DownstreamImpact(snapshot, scenario.TargetNodeId, maxDepth: MaxPropagationHops);

        // Include the target node itself at full propagated strength (1.0) alongside everything downstream.
        var affected = new List<(Guid NodeId, decimal PropagatedStrength, int HopDistance)>
        {
            (targetNode.Id, FullStrength, 0)
        };
        affected.AddRange(impacted.Select(i => (i.NodeId, i.PropagatedStrength, i.HopDistance)));

        var totalSimulatedDays = scenario.DurationDays + RecoveryWindow(targetNode.Criticality);
        var steps = new List<SimulationStep>();
        decimal totalRevenueImpact = 0m;
        decimal peakImpact = 0m;

        foreach (var (nodeId, propagatedStrength, hopDistance) in affected)
        {
            var node = snapshot.Find(nodeId);
            if (node is null) continue;

            var recoveryWindow = RecoveryWindow(node.Criticality);
            var rampInDays = Math.Min(MaxRampInDays, scenario.DurationDays);

            for (var day = 0; day <= totalSimulatedDays; day++)
            {
                decimal dailyFraction;
                if (day <= rampInDays)
                {
                    dailyFraction = rampInDays == 0 ? 1m : (decimal)day / rampInDays;
                }
                else if (day <= scenario.DurationDays)
                {
                    dailyFraction = 1m;
                }
                else
                {
                    var daysIntoRecovery = day - scenario.DurationDays;
                    dailyFraction = recoveryWindow == 0
                        ? 0m
                        : Math.Max(0m, 1m - (decimal)daysIntoRecovery / recoveryWindow);
                }

                var impactScore = Math.Round(scenario.Severity * propagatedStrength * dailyFraction, ImpactScoreDecimals);
                if (impactScore <= 0m) continue;

                peakImpact = Math.Max(peakImpact, impactScore);
                var status = impactScore >= CriticalImpactThreshold ? "Critical" : impactScore >= WarningImpactThreshold ? "Warning" : "Healthy";
                var dailyRevenueImpact = Math.Round(node.RevenueExposure / DaysPerYear * impactScore, CurrencyDecimals);
                totalRevenueImpact += dailyRevenueImpact;

                steps.Add(new SimulationStep(simulationRunId, day, nodeId, impactScore, status, dailyRevenueImpact));
            }
        }

        var recoveryTimeDays = scenario.DurationDays + RecoveryWindow(targetNode.Criticality);
        var summary = BuildSummary(scenario, targetNode.DisplayName, affected.Count, totalRevenueImpact, recoveryTimeDays);

        var result = new SimulationResult(
            simulationRunId,
            totalRevenueImpact: Math.Round(totalRevenueImpact, CurrencyDecimals),
            peakDailyImpactScore: peakImpact,
            affectedNodeCount: affected.Count,
            recoveryTimeDays: recoveryTimeDays,
            summary: summary);

        return new SimulationOutput(steps, result);
    }

    /// <summary>Higher-criticality nodes take longer to recover: 10 days per criticality point.</summary>
    private static int RecoveryWindow(int criticality) =>
        Math.Max(MinimumRecoveryWindowDays, criticality * RecoveryDaysPerCriticalityPoint);

    private static string BuildSummary(Scenario scenario, string targetName, int affectedCount, decimal totalRevenueImpact, int recoveryDays) =>
        $"{scenario.EventType} on {targetName} at {scenario.Severity:P0} severity for {scenario.DurationDays} days " +
        $"propagates to {affectedCount} graph node(s), with an estimated ${totalRevenueImpact:N0} total revenue impact " +
        $"and a projected {recoveryDays}-day recovery window.";
}

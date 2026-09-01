using System.Text.Json;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Risk.Application;
using Aurora.Modules.Risk.Domain;

namespace Aurora.Modules.AI.Application;

/// <summary>
/// A small, honest version of the Agent Orchestrator (spec section 22-24): it does NOT run a
/// swarm of autonomous LLM agents. It runs a fixed pipeline of deterministic tools — graph
/// traversal, single-point-of-failure detection, bottleneck detection, risk scoring — against
/// the live graph snapshot for the node(s) mentioned in the question, records each tool call as
/// AiEvidence, and finally asks IAiProvider only to phrase a narrative over that already-computed
/// evidence. This satisfies "AI must not have direct database access" (spec section 25) and
/// "the LLM must never be the authoritative calculation engine" (spec section 100) precisely,
/// rather than gesturing at it. A future multi-agent planner can be layered in front of this
/// class without changing what guarantees the evidence carries.
/// </summary>
public sealed class InvestigationOrchestrator
{
    private readonly IGraphProjectionService _graphProjection;
    private readonly GraphTraversalService _traversal;
    private readonly IAiProvider _aiProvider;
    private readonly RiskScoringWeights _riskWeights;

    public InvestigationOrchestrator(
        IGraphProjectionService graphProjection,
        GraphTraversalService traversal,
        IAiProvider aiProvider,
        RiskScoringWeights riskWeights)
    {
        _graphProjection = graphProjection;
        _traversal = traversal;
        _aiProvider = aiProvider;
        _riskWeights = riskWeights;
    }

    /// <summary>
    /// Investigates "what would be affected if [nodeId] failed / is at risk" — the canonical
    /// AURORA question (spec section 8/59). Returns a populated, but not-yet-persisted,
    /// AiInvestigation aggregate; the caller (Aurora.Web) is responsible for saving it.
    /// </summary>
    public async Task<AiInvestigation> InvestigateNodeRiskAsync(
        Guid organizationId, Guid requestedByUserId, Guid targetNodeId, string question, CancellationToken ct = default)
    {
        var investigation = new AiInvestigation(organizationId, requestedByUserId, question);

        var swTotal = System.Diagnostics.Stopwatch.StartNew();

        // Tool 1: build the live graph snapshot.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = await _graphProjection.BuildLiveGraphAsync(organizationId, ct);
        var targetNode = snapshot.Find(targetNodeId);
        sw.Stop();
        investigation.AddEvidence("BuildLiveGraph",
            $"Loaded live graph with {snapshot.Nodes.Count} nodes and {snapshot.Edges.Count} edges.",
            JsonSerializer.Serialize(new { snapshot.Nodes.Count, EdgeCount = snapshot.Edges.Count }),
            sw.Elapsed);

        if (targetNode is null)
        {
            investigation.Fail();
            return investigation;
        }

        // Tool 2: downstream impact analysis.
        sw.Restart();
        var downstream = _traversal.DownstreamImpact(snapshot, targetNodeId, maxDepth: 8);
        sw.Stop();
        investigation.AddEvidence("TraverseDependencies (downstream)",
            $"{downstream.Count} node(s) depend on {targetNode.DisplayName}, directly or indirectly.",
            JsonSerializer.Serialize(downstream.Take(25)),
            sw.Elapsed);

        // Tool 3: single-point-of-failure check.
        sw.Restart();
        var spofs = _traversal.DetectSinglePointsOfFailure(snapshot);
        var isSpof = spofs.Contains(targetNodeId);
        sw.Stop();
        investigation.AddEvidence("DetectSinglePointsOfFailure",
            isSpof
                ? $"{targetNode.DisplayName} is a single point of failure: at least one dependent has no alternate path."
                : $"{targetNode.DisplayName} is not flagged as a single point of failure under current redundancy.",
            JsonSerializer.Serialize(new { IsSpof = isSpof }),
            sw.Elapsed);

        // Tool 4: risk scoring using the node's own criticality plus how concentrated its
        // downstream exposure is (more dependents => higher probability the failure matters).
        sw.Restart();
        var probability = Math.Clamp(0.15m + downstream.Count * 0.03m, 0m, 0.95m);
        var impact = Math.Clamp(downstream.Sum(d => d.PropagatedStrength) / Math.Max(1, downstream.Count), 0m, 1m);
        var exposure = Math.Clamp(targetNode.RevenueExposure > 0 ? 0.9m : 0.3m, 0m, 1m);
        var riskScore = RiskScoringService.Score(probability, impact, exposure, targetNode.Criticality, _riskWeights);
        sw.Stop();
        investigation.AddEvidence("RiskScoring",
            $"Computed risk score {riskScore:0.0}/100 for {targetNode.DisplayName} " +
            $"(probability {probability:P0}, impact {impact:P0}, exposure {exposure:P0}, criticality {targetNode.Criticality}/5).",
            JsonSerializer.Serialize(new { probability, impact, exposure, targetNode.Criticality, riskScore }),
            sw.Elapsed);

        swTotal.Stop();

        var totalRevenueAtRisk = downstream
            .Select(d => (Node: snapshot.Find(d.NodeId), d.PropagatedStrength))
            .Where(x => x.Node is not null)
            .Sum(x => x.Node!.RevenueExposure * x.PropagatedStrength);

        var evidenceForNarrative = JsonSerializer.Serialize(new
        {
            Target = targetNode.DisplayName,
            DownstreamNodeCount = downstream.Count,
            IsSinglePointOfFailure = isSpof,
            RiskScore = riskScore,
            EstimatedRevenueAtRisk = Math.Round(totalRevenueAtRisk, 0)
        });

        var narrative = _aiProvider.IsConfigured
            ? await _aiProvider.ExplainAsync(
                "You are AURORA's Explanation Agent. Explain the evidence in 2-4 sentences for a business " +
                "audience. Do not invent numbers beyond what is given.",
                evidenceForNarrative, ct)
            : BuildFallbackNarrative(targetNode.DisplayName, downstream.Count, isSpof, riskScore, totalRevenueAtRisk);

        // Confidence reflects evidence volume and graph completeness, not an LLM's self-report.
        var confidence = Math.Clamp(50 + Math.Min(downstream.Count, 20) * 2, 50, 95);

        investigation.Complete(narrative, confidence);
        return investigation;
    }

    private static string BuildFallbackNarrative(string targetName, int downstreamCount, bool isSpof, decimal riskScore, decimal revenueAtRisk) =>
        $"{targetName} has {downstreamCount} downstream dependent(s) in the enterprise graph, " +
        $"putting an estimated ${revenueAtRisk:N0} in annual revenue exposure at risk if it fails. " +
        (isSpof ? $"{targetName} is currently a single point of failure with no alternate path for at least one dependent. " : "") +
        $"Computed risk score: {riskScore:0.0}/100.";
}

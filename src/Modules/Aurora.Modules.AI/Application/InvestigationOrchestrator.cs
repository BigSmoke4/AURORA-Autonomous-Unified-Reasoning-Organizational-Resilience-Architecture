using System.Diagnostics;
using System.Text.Json;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Risk.Application;
using Aurora.Modules.Risk.Domain;

namespace Aurora.Modules.AI.Application;

/// <summary>
/// The investigation pipeline (spec sections 22-24). It is a fixed sequence of deterministic tools:
/// graph traversal, single-point-of-failure screening, and risk scoring over the live graph snapshot.
/// Each tool call is recorded as AiEvidence. Only then is the language model asked to phrase a
/// narrative over those results. If the model is unavailable, the deterministic narrative is used
/// and the investigation records that choice. The model is never the source of any number.
/// </summary>
public sealed class InvestigationOrchestrator
{
    public const string DeterministicModelVersion = "aurora-deterministic-narrative-v1";
    public const int DownstreamDepth = 8;
    public const int EvidenceSampleSize = 25;

    // Heuristic proxies for the risk tool. They are named and labelled as proxies in the evidence,
    // so nobody mistakes them for fitted probabilities.
    public const decimal BaseFailureProxy = 0.15m;
    public const decimal FailureProxyPerDependent = 0.03m;
    public const decimal FailureProxyCap = 0.95m;
    public const decimal ExposureWithRevenue = 0.9m;
    public const decimal ExposureWithoutRevenue = 0.3m;

    public const int ConfidenceBase = 50;
    public const int ConfidencePerDependent = 2;
    public const int ConfidenceDependentCap = 20;
    public const int ConfidenceCap = 95;

    private const string SystemPrompt =
        "You are AURORA's Explanation Agent. Explain the evidence in 2-4 sentences for a business audience. " +
        "Do not invent numbers beyond what is given.";

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
    /// Investigates "what would be affected if [node] failed" (spec section 8/59). Returns a populated,
    /// unsaved AiInvestigation. The caller persists it in the same unit of work as its audit entry.
    /// </summary>
    public async Task<AiInvestigation> InvestigateNodeRiskAsync(
        Guid organizationId, Guid requestedByUserId, Guid targetNodeId, string question, CancellationToken ct = default)
    {
        var investigation = new AiInvestigation(organizationId, requestedByUserId, question);

        var sw = Stopwatch.StartNew();
        var snapshot = await _graphProjection.BuildLiveGraphAsync(organizationId, ct);
        var targetNode = snapshot.Find(targetNodeId);
        sw.Stop();
        investigation.AddEvidence("BuildLiveGraph",
            $"Loaded live graph with {snapshot.Nodes.Count} nodes and {snapshot.Edges.Count} edges.",
            JsonSerializer.Serialize(new { NodeCount = snapshot.Nodes.Count, EdgeCount = snapshot.Edges.Count }),
            sw.Elapsed);

        if (targetNode is null)
        {
            investigation.Fail();
            return investigation;
        }

        sw.Restart();
        var downstream = _traversal.DownstreamImpact(snapshot, targetNodeId, DownstreamDepth);
        sw.Stop();
        investigation.AddEvidence("TraverseDependencies (downstream)",
            $"{downstream.Count} node(s) depend on {targetNode.DisplayName}, directly or indirectly.",
            JsonSerializer.Serialize(downstream.Take(EvidenceSampleSize)),
            sw.Elapsed);

        sw.Restart();
        var isSingleSourced = _traversal.DetectSingleSourcedNodes(snapshot).Contains(targetNodeId);
        sw.Stop();
        investigation.AddEvidence("SupplyMinimumCut",
            isSingleSourced
                ? $"{targetNode.DisplayName} is a single point of failure: removing it alone cuts at least one dependent off from every supplier of its type."
                : $"{targetNode.DisplayName} is not flagged as single-sourced under current redundancy.",
            JsonSerializer.Serialize(new { IsSingleSourced = isSingleSourced }),
            sw.Elapsed);

        sw.Restart();
        var probability = Math.Clamp(BaseFailureProxy + downstream.Count * FailureProxyPerDependent, 0m, FailureProxyCap);
        var impact = Math.Clamp(downstream.Sum(d => d.PropagatedStrength) / Math.Max(1, downstream.Count), 0m, 1m);
        var exposure = targetNode.RevenueExposure > 0m ? ExposureWithRevenue : ExposureWithoutRevenue;
        var riskScore = RiskScoringService.Score(probability, impact, exposure, targetNode.Criticality, _riskWeights);
        sw.Stop();
        investigation.AddEvidence("RiskScoring",
            $"Computed risk score {riskScore:0.0}/100 for {targetNode.DisplayName} using heuristic proxy inputs " +
            $"(probability {probability:P0}, impact {impact:P0}, exposure {exposure:P0}, criticality {targetNode.Criticality}/5).",
            JsonSerializer.Serialize(new { probability, impact, exposure, targetNode.Criticality, riskScore }),
            sw.Elapsed);

        var totalRevenueAtRisk = downstream
            .Select(d => (Node: snapshot.Find(d.NodeId), d.PropagatedStrength))
            .Where(x => x.Node is not null)
            .Sum(x => x.Node!.RevenueExposure * x.PropagatedStrength);

        var evidenceForNarrative = JsonSerializer.Serialize(new
        {
            Target = targetNode.DisplayName,
            DownstreamNodeCount = downstream.Count,
            IsSingleSourced = isSingleSourced,
            RiskScore = riskScore,
            EstimatedRevenueAtRisk = Math.Round(totalRevenueAtRisk, 0),
        });

        var generated = await _aiProvider.TryExplainAsync(SystemPrompt, evidenceForNarrative, ct);
        var narrative = generated?.Text
            ?? BuildFallbackNarrative(targetNode.DisplayName, downstream.Count, isSingleSourced, riskScore, totalRevenueAtRisk);

        var confidence = ConfidenceBase + Math.Min(downstream.Count, ConfidenceDependentCap) * ConfidencePerDependent;
        investigation.Complete(
            narrative,
            Math.Min(confidence, ConfidenceCap),
            generated is null ? NarrativeSource.Deterministic : NarrativeSource.Llm,
            generated?.ModelVersion ?? DeterministicModelVersion);
        return investigation;
    }

    private static string BuildFallbackNarrative(string targetName, int downstreamCount, bool isSingleSourced, decimal riskScore, decimal revenueAtRisk) =>
        $"{targetName} has {downstreamCount} downstream dependent(s) in the enterprise graph, " +
        $"putting an estimated {revenueAtRisk:C0} in revenue exposure at risk if it fails. " +
        (isSingleSourced ? $"{targetName} is a single point of failure: removing it alone cuts at least one dependent off from every supplier of its type. " : string.Empty) +
        $"Computed risk score: {riskScore:0.0}/100.";
}

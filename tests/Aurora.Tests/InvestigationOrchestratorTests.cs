using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Risk.Domain;
using Xunit;

namespace Aurora.Tests;

public class InvestigationOrchestratorTests
{
    private readonly Guid _org = Guid.NewGuid();
    private readonly GraphNode _supplier;
    private readonly GraphNode _factory;
    private readonly GraphSnapshot _snapshot;

    public InvestigationOrchestratorTests()
    {
        _supplier = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "Northbridge Components", 5, 0m);
        _factory = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Factory Alpha", 4, 1_200_000m);
        var edge = new GraphEdge(_org, _supplier.Id, _factory.Id, GraphRelationshipType.Supplies, 0.9m, 5);
        _snapshot = new GraphSnapshot(_org, new[] { _supplier, _factory }, new[] { edge });
    }

    private InvestigationOrchestrator CreateOrchestrator(IAiProvider provider) =>
        new(new FixedGraphProjection(_snapshot), new GraphTraversalService(), provider, new RiskScoringWeights());

    [Fact]
    public async Task Investigation_RunsFourDeterministicToolsAndFallsBackToTheDeterministicNarrative()
    {
        var orchestrator = CreateOrchestrator(new FakeAiProvider(null));

        var investigation = await orchestrator.InvestigateNodeRiskAsync(_org, Guid.NewGuid(), _supplier.Id, "What if it fails?");

        Assert.Equal(InvestigationStatus.Completed, investigation.Status);
        Assert.Equal(4, investigation.Evidence.Count);
        Assert.Equal(NarrativeSource.Deterministic, investigation.NarrativeSource);
        Assert.Equal(InvestigationOrchestrator.DeterministicModelVersion, investigation.ModelVersion);
        Assert.Contains("Northbridge Components", investigation.NarrativeExplanation!);
        Assert.InRange(investigation.Confidence, 0m, (decimal)InvestigationOrchestrator.ConfidenceCap);
    }

    [Fact]
    public async Task Investigation_UsesModelText_ButRecordsTheModelThatWroteIt()
    {
        var orchestrator = CreateOrchestrator(new FakeAiProvider(new NarrativeResult("A supplier outage would reach the factory.", "fake-model")));

        var investigation = await orchestrator.InvestigateNodeRiskAsync(_org, Guid.NewGuid(), _supplier.Id, "What if it fails?");

        Assert.Equal(NarrativeSource.Llm, investigation.NarrativeSource);
        Assert.Equal("fake-model", investigation.ModelVersion);
        Assert.Equal("A supplier outage would reach the factory.", investigation.NarrativeExplanation);
    }

    [Fact]
    public async Task UnknownTarget_FailsTheInvestigation_WithOnlyTheGraphLoadEvidence()
    {
        var orchestrator = CreateOrchestrator(new FakeAiProvider(null));

        var investigation = await orchestrator.InvestigateNodeRiskAsync(_org, Guid.NewGuid(), Guid.NewGuid(), "Unknown?");

        Assert.Equal(InvestigationStatus.Failed, investigation.Status);
        Assert.Single(investigation.Evidence);
    }

    private sealed class FixedGraphProjection(GraphSnapshot snapshot) : IGraphProjectionService
    {
        public Task<GraphSnapshot> BuildLiveGraphAsync(Guid organizationId, CancellationToken ct = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class FakeAiProvider(NarrativeResult? result) : IAiProvider
    {
        public bool IsConfigured => result is not null;

        public string ModelVersion => "fake";

        public Task<NarrativeResult?> TryExplainAsync(string systemPrompt, string evidenceJson, CancellationToken ct = default) =>
            Task.FromResult(result);
    }
}

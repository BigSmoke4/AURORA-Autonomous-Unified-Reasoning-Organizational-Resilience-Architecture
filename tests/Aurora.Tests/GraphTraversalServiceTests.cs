using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Xunit;

namespace Aurora.Tests;

public class GraphTraversalServiceTests
{
    private readonly Guid _org = Guid.NewGuid();

    private (GraphSnapshot Snapshot, GraphNode A, GraphNode B, GraphNode C, GraphNode D) BuildChainGraph()
    {
        // A --DEPENDS_ON--> B --DEPENDS_ON--> C, and D --DEPENDS_ON--> B (fan-in on B)
        var a = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "A", 3, 1000m);
        var b = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "B", 3, 2000m);
        var c = new GraphNode(_org, GraphNodeType.Product, Guid.NewGuid(), "C", 3, 3000m);
        var d = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "D", 3, 500m);

        var edges = new List<GraphEdge>
        {
            new(_org, a.Id, b.Id, GraphRelationshipType.Supplies, 0.8m, 3),
            new(_org, b.Id, c.Id, GraphRelationshipType.Produces, 0.6m, 3),
            new(_org, d.Id, b.Id, GraphRelationshipType.Supplies, 0.5m, 2),
        };

        var snapshot = new GraphSnapshot(_org, new[] { a, b, c, d }, edges);
        return (snapshot, a, b, c, d);
    }

    [Fact]
    public void BreadthFirstTraversal_VisitsAllReachableNodes_InOrder()
    {
        var (snapshot, a, b, c, _) = BuildChainGraph();
        var service = new GraphTraversalService();

        var order = service.BreadthFirstTraversal(snapshot, a.Id);

        Assert.Equal(new[] { a.Id, b.Id, c.Id }, order);
    }

    [Fact]
    public void DownstreamImpact_FindsNodesThatDependOnTarget_ViaOutgoingEdges()
    {
        var (snapshot, _, b, c, _) = BuildChainGraph();
        var service = new GraphTraversalService();

        // Edges are A->B and D->B ("A supplies B", "D supplies B") and B->C ("B produces C").
        // If B fails, C depends on B (edge B->C), so C is downstream of B. A and D are what B
        // depends ON (upstream), not downstream — they are unaffected by B failing.
        var impacted = service.DownstreamImpact(snapshot, b.Id);
        var impactedIds = impacted.Select(i => i.NodeId).ToHashSet();

        Assert.Contains(c.Id, impactedIds);
    }

    [Fact]
    public void UpstreamDependencies_FindsNodesThatTargetDependsOn_ViaIncomingEdges()
    {
        var (snapshot, a, b, _, d) = BuildChainGraph();
        var service = new GraphTraversalService();

        // B depends on A and D (edges A->B, D->B), so both are upstream of B.
        var upstream = service.UpstreamDependencies(snapshot, b.Id);
        var upstreamIds = upstream.Select(i => i.NodeId).ToHashSet();

        Assert.Contains(a.Id, upstreamIds);
        Assert.Contains(d.Id, upstreamIds);
    }

    [Fact]
    public void ShortestDependencyPath_FindsPath_WhenOneExists()
    {
        var (snapshot, a, _, c, _) = BuildChainGraph();
        var service = new GraphTraversalService();

        var result = service.ShortestDependencyPath(snapshot, a.Id, c.Id);

        Assert.True(result.Found);
        Assert.Equal(a.Id, result.NodeIds.First());
        Assert.Equal(c.Id, result.NodeIds.Last());
    }

    [Fact]
    public void ShortestDependencyPath_ReturnsNotFound_WhenNoPathExists()
    {
        var (snapshot, _, _, c, d) = BuildChainGraph();
        var service = new GraphTraversalService();

        // C has no outgoing edges, so there is no path from C to D.
        var result = service.ShortestDependencyPath(snapshot, c.Id, d.Id);

        Assert.False(result.Found);
        Assert.Empty(result.NodeIds);
    }

    [Fact]
    public void DetectSinglePointsOfFailure_FlagsConcentratedDependency()
    {
        // Two suppliers of the same type feeding one factory => not a SPOF for that edge.
        var s1 = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "S1", 3, 100m);
        var s2 = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "S2", 3, 100m);
        var soleSupplier = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "SoleSupplier", 5, 500m);
        var factoryWithRedundancy = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "FactoryRedundant", 3, 1000m);
        var factoryWithoutRedundancy = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "FactorySingleSourced", 4, 2000m);

        var edges = new List<GraphEdge>
        {
            new(_org, s1.Id, factoryWithRedundancy.Id, GraphRelationshipType.Supplies, 0.5m, 3),
            new(_org, s2.Id, factoryWithRedundancy.Id, GraphRelationshipType.Supplies, 0.5m, 3),
            new(_org, soleSupplier.Id, factoryWithoutRedundancy.Id, GraphRelationshipType.Supplies, 0.9m, 5),
        };

        var snapshot = new GraphSnapshot(_org, new[] { s1, s2, soleSupplier, factoryWithRedundancy, factoryWithoutRedundancy }, edges);
        var service = new GraphTraversalService();

        var spofs = service.DetectSinglePointsOfFailure(snapshot);

        Assert.Contains(soleSupplier.Id, spofs);
    }
}

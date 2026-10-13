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
    public void DetectSingleSourcedNodes_FlagsConcentratedDependency()
    {
        // Two suppliers of the same type feeding one factory => not single-sourced for that edge.
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

        var singleSourced = service.DetectSingleSourcedNodes(snapshot);

        Assert.Contains(soleSupplier.Id, singleSourced);
    }

    [Fact]
    public void DownstreamImpact_ReportsStrongestPath_NotFirstPathFound()
    {
        // X -> Y directly with a weak edge (0.1), and X -> Z -> Y with two strong edges (0.9 * 0.9).
        // Max-product propagation must report the strongest route to Y, not the direct one.
        var x = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "X", 3, 100m);
        var y = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Y", 3, 100m);
        var z = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "Z", 3, 100m);

        var edges = new List<GraphEdge>
        {
            new(_org, x.Id, y.Id, GraphRelationshipType.Supplies, 0.1m, 3),
            new(_org, x.Id, z.Id, GraphRelationshipType.Supplies, 0.9m, 3),
            new(_org, z.Id, y.Id, GraphRelationshipType.Supplies, 0.9m, 3),
        };

        var snapshot = new GraphSnapshot(_org, new[] { x, y, z }, edges);
        var service = new GraphTraversalService();

        var impactY = service.DownstreamImpact(snapshot, x.Id).Single(i => i.NodeId == y.Id);

        Assert.Equal(0.81m, impactY.PropagatedStrength);
        Assert.Equal(2, impactY.HopDistance);
    }

    [Fact]
    public void DownstreamImpact_DoesNotExpandBeyondMaxDepth()
    {
        // P -> Q -> R -> S. With maxDepth 2, R (two hops away) is reported but S (three hops) is not.
        var p = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "P", 3, 100m);
        var q = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Q", 3, 100m);
        var r = new GraphNode(_org, GraphNodeType.Product, Guid.NewGuid(), "R", 3, 100m);
        var s = new GraphNode(_org, GraphNodeType.Customer, Guid.NewGuid(), "S", 3, 100m);

        var edges = new List<GraphEdge>
        {
            new(_org, p.Id, q.Id, GraphRelationshipType.Supplies, 0.9m, 3),
            new(_org, q.Id, r.Id, GraphRelationshipType.Produces, 0.9m, 3),
            new(_org, r.Id, s.Id, GraphRelationshipType.Supplies, 0.9m, 3),
        };

        var snapshot = new GraphSnapshot(_org, new[] { p, q, r, s }, edges);
        var service = new GraphTraversalService();

        var impactedIds = service.DownstreamImpact(snapshot, p.Id, maxDepth: 2).Select(i => i.NodeId).ToHashSet();

        Assert.Contains(r.Id, impactedIds);
        Assert.DoesNotContain(s.Id, impactedIds);
    }

    [Fact]
    public void DownstreamImpact_DropsSignalsBelowMinimumSignal()
    {
        // 0.01 is below GraphTraversalService.MinimumSignal (0.02), so the impact is negligible.
        var p = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "P", 3, 100m);
        var q = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "Q", 3, 100m);

        var edges = new List<GraphEdge>
        {
            new(_org, p.Id, q.Id, GraphRelationshipType.Supplies, 0.01m, 3),
        };

        var snapshot = new GraphSnapshot(_org, new[] { p, q }, edges);
        var service = new GraphTraversalService();

        Assert.Empty(service.DownstreamImpact(snapshot, p.Id));
    }

    [Fact]
    public void DownstreamImpact_ExcludesStartNode_WhenGraphHasCycle()
    {
        // A -> B -> A. A failing impacts B, but A must never be reported as impacted by itself.
        var a = new GraphNode(_org, GraphNodeType.Supplier, Guid.NewGuid(), "A", 3, 100m);
        var b = new GraphNode(_org, GraphNodeType.Factory, Guid.NewGuid(), "B", 3, 100m);

        var edges = new List<GraphEdge>
        {
            new(_org, a.Id, b.Id, GraphRelationshipType.Supplies, 0.8m, 3),
            new(_org, b.Id, a.Id, GraphRelationshipType.Supplies, 0.8m, 3),
        };

        var snapshot = new GraphSnapshot(_org, new[] { a, b }, edges);
        var service = new GraphTraversalService();

        var impactedIds = service.DownstreamImpact(snapshot, a.Id).Select(i => i.NodeId).ToList();

        Assert.Contains(b.Id, impactedIds);
        Assert.DoesNotContain(a.Id, impactedIds);
    }
}

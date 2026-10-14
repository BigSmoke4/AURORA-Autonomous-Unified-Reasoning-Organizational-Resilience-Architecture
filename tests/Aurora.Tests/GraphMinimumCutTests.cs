using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Xunit;

namespace Aurora.Tests;

/// <summary>Vertex minimum-cut analysis: how many suppliers of one type must fail to cut a dependent off.</summary>
public sealed class GraphMinimumCutTests
{
    private readonly Guid _org = Guid.NewGuid();

    private GraphNode Node(GraphNodeType type, string name) =>
        new(_org, type, Guid.NewGuid(), name, 3, 1000m);

    private GraphEdge Supplies(GraphNode from, GraphNode to) =>
        new(_org, from.Id, to.Id, GraphRelationshipType.Supplies, 0.5m, 3);

    [Fact]
    public void SoleSupplier_IsTheOnlyCutOfItsDependent()
    {
        var supplier = Node(GraphNodeType.Supplier, "Sole");
        var factory = Node(GraphNodeType.Factory, "Factory");
        var snapshot = new GraphSnapshot(_org, new[] { supplier, factory }, new[] { Supplies(supplier, factory) });

        var cut = new GraphTraversalService().MinimumSupplyCut(snapshot, factory.Id, GraphNodeType.Supplier);

        Assert.Equal(1, cut.CutSize);
        Assert.Equal(new[] { supplier.Id }, cut.CutNodeIds);
    }

    [Fact]
    public void TwoDirectSuppliers_NeedBothToFailToCutTheDependent()
    {
        var s1 = Node(GraphNodeType.Supplier, "S1");
        var s2 = Node(GraphNodeType.Supplier, "S2");
        var factory = Node(GraphNodeType.Factory, "Factory");
        var snapshot = new GraphSnapshot(_org, new[] { s1, s2, factory }, new[] { Supplies(s1, factory), Supplies(s2, factory) });

        var cut = new GraphTraversalService().MinimumSupplyCut(snapshot, factory.Id, GraphNodeType.Supplier);

        Assert.Equal(2, cut.CutSize);
        Assert.Equal(new[] { s1.Id, s2.Id }.OrderBy(id => id), cut.CutNodeIds.OrderBy(id => id));
    }

    [Fact]
    public void RedundancyThroughAnIntermediateNode_IsCountedAcrossHops()
    {
        // S1 -> Factory -> Product, and S2 -> Product directly. Product keeps a supplier if either one fails, so
        // its cut is {S1, S2}. The old one-hop screen flagged S2 because Product's only direct supplier is S2 and it
        // ignored the route through Factory. S1 is a real single point of failure, because Factory depends on it alone.
        var s1 = Node(GraphNodeType.Supplier, "S1");
        var s2 = Node(GraphNodeType.Supplier, "S2");
        var factory = Node(GraphNodeType.Factory, "Factory");
        var product = Node(GraphNodeType.Product, "Product");
        var edges = new[] { Supplies(s1, factory), Supplies(s2, product), Supplies(factory, product) };
        var snapshot = new GraphSnapshot(_org, new[] { s1, s2, factory, product }, edges);
        var service = new GraphTraversalService();

        var productCut = service.MinimumSupplyCut(snapshot, product.Id, GraphNodeType.Supplier);
        var findings = service.SinglePointsOfFailure(snapshot);
        var flagged = service.DetectSingleSourcedNodes(snapshot);

        Assert.Equal(2, productCut.CutSize);
        Assert.DoesNotContain(findings, f => f.DependentNodeId == product.Id && f.SourceType == GraphNodeType.Supplier);
        // Factory is itself a source of Product, and Product has only one factory, so that factory is a real single point.
        Assert.Contains(findings, f => f.DependentNodeId == product.Id && f.SourceType == GraphNodeType.Factory && f.CutNodeIds.Single() == factory.Id);
        // Flagged nodes are the members of every size-one cut: S1 (Factory depends on it alone) and Factory (Product depends on it alone).
        Assert.Equal(new[] { s1.Id, factory.Id }.OrderBy(id => id), flagged.OrderBy(id => id));
    }

    [Fact]
    public void DependentWithNoSupplierOfThatType_HasEmptyCut()
    {
        var supplier = Node(GraphNodeType.Supplier, "Supplier");
        var customer = Node(GraphNodeType.Customer, "Customer");
        var snapshot = new GraphSnapshot(_org, new[] { supplier, customer }, Array.Empty<GraphEdge>());

        var cut = new GraphTraversalService().MinimumSupplyCut(snapshot, customer.Id, GraphNodeType.Supplier);

        Assert.Equal(0, cut.CutSize);
        Assert.Empty(cut.CutNodeIds);
    }

    [Fact]
    public void SinglePointsOfFailure_OnlyReportsCutsOfSizeOne()
    {
        var s1 = Node(GraphNodeType.Supplier, "S1");
        var s2 = Node(GraphNodeType.Supplier, "S2");
        var redundant = Node(GraphNodeType.Factory, "Redundant");
        var fragile = Node(GraphNodeType.Factory, "Fragile");
        var edges = new[] { Supplies(s1, redundant), Supplies(s2, redundant), Supplies(s1, fragile) };
        var snapshot = new GraphSnapshot(_org, new[] { s1, s2, redundant, fragile }, edges);

        var findings = new GraphTraversalService().SinglePointsOfFailure(snapshot);

        Assert.Contains(findings, f => f.DependentNodeId == fragile.Id && f.CutNodeIds.Single() == s1.Id);
        Assert.DoesNotContain(findings, f => f.DependentNodeId == redundant.Id);
    }
}

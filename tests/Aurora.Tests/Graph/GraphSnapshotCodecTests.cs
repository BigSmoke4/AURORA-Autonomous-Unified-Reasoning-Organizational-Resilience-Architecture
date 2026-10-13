using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Xunit;

namespace Aurora.Tests.Graph;

public sealed class GraphSnapshotCodecTests
{
    private readonly Guid _org = Guid.NewGuid();

    [Fact]
    public void RoundTripKeepsEveryIdSoScenariosStillResolve()
    {
        var supplier = GraphNode.Restore(Guid.NewGuid(), _org, GraphNodeType.Supplier, Guid.NewGuid(), "Northbridge", 4, 1200m, "Healthy");
        var factory = GraphNode.Restore(Guid.NewGuid(), _org, GraphNodeType.Factory, Guid.NewGuid(), "Plant 1", 5, 9000m, "Degraded");
        var edge = GraphEdge.Restore(Guid.NewGuid(), _org, supplier.Id, factory.Id, GraphRelationshipType.Supplies, 0.5m, 3, "Tier 1");
        var original = new GraphSnapshot(_org, new[] { supplier, factory }, new[] { edge });

        var restored = GraphSnapshotCodec.Deserialize(_org, GraphSnapshotCodec.Serialize(original));

        Assert.Equal(original.Nodes.Keys.OrderBy(id => id), restored.Nodes.Keys.OrderBy(id => id));
        Assert.Equal("Degraded", restored.Find(factory.Id)?.Status);
        Assert.Equal(edge.Id, Assert.Single(restored.Edges).Id);
        Assert.Equal("Tier 1", Assert.Single(restored.Edges).Metadata);
    }

    [Fact]
    public void SerializationIsIndependentOfInsertionOrder()
    {
        var a = GraphNode.Restore(Guid.NewGuid(), _org, GraphNodeType.Supplier, Guid.NewGuid(), "A", 1, 1m, "Healthy");
        var b = GraphNode.Restore(Guid.NewGuid(), _org, GraphNodeType.Supplier, Guid.NewGuid(), "B", 1, 1m, "Healthy");

        var forward = GraphSnapshotCodec.Serialize(new GraphSnapshot(_org, new[] { a, b }, Array.Empty<GraphEdge>()));
        var reversed = GraphSnapshotCodec.Serialize(new GraphSnapshot(_org, new[] { b, a }, Array.Empty<GraphEdge>()));

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void UnknownSchemaVersionIsRejected()
    {
        var json = "{\"SchemaVersion\":999,\"Nodes\":[],\"Edges\":[]}";

        Assert.Throws<InvalidDataException>(() => GraphSnapshotCodec.Deserialize(_org, json));
    }
}

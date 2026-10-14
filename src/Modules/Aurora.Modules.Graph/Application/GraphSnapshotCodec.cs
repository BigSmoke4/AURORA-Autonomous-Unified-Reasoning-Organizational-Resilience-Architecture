using System.Text.Json;
using Aurora.Modules.Graph.Domain;

namespace Aurora.Modules.Graph.Application;

/// <summary>
/// Stores a graph snapshot as JSON and rebuilds it with the same node and edge ids. Ids must survive the round trip,
/// because scenarios refer to nodes by id. Output is ordered by id, so the same graph always serializes to the same text.
/// </summary>
public static class GraphSnapshotCodec
{
    public const int SchemaVersion = 1;

    private sealed record NodePayload(
        Guid Id, GraphNodeType NodeType, Guid EntityId, string DisplayName, int Criticality, decimal RevenueExposure, string Status);

    private sealed record EdgePayload(
        Guid Id, Guid SourceNodeId, Guid TargetNodeId, GraphRelationshipType RelationshipType, decimal Strength, int Criticality, string? Metadata);

    private sealed record Payload(int SchemaVersion, IReadOnlyList<NodePayload> Nodes, IReadOnlyList<EdgePayload> Edges);

    public static string Serialize(GraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var nodes = snapshot.Nodes.Values
            .OrderBy(n => n.Id)
            .Select(n => new NodePayload(n.Id, n.NodeType, n.EntityId, n.DisplayName, n.Criticality, n.RevenueExposure, n.Status))
            .ToList();
        var edges = snapshot.Edges
            .OrderBy(e => e.Id)
            .Select(e => new EdgePayload(e.Id, e.SourceNodeId, e.TargetNodeId, e.RelationshipType, e.Strength, e.Criticality, e.Metadata))
            .ToList();
        return JsonSerializer.Serialize(new Payload(SchemaVersion, nodes, edges));
    }

    public static GraphSnapshot Deserialize(Guid organizationId, string json)
    {
        var payload = JsonSerializer.Deserialize<Payload>(json)
            ?? throw new InvalidDataException("The graph snapshot is empty.");
        if (payload.SchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException($"Graph snapshot schema {payload.SchemaVersion} is not supported.");
        }
        if (payload.Nodes is null || payload.Edges is null)
        {
            throw new InvalidDataException("The graph snapshot is missing its nodes or edges.");
        }

        var nodes = payload.Nodes.Select(n => GraphNode.Restore(
            n.Id, organizationId, n.NodeType, n.EntityId, n.DisplayName, n.Criticality, n.RevenueExposure, n.Status));
        var edges = payload.Edges.Select(e => GraphEdge.Restore(
            e.Id, organizationId, e.SourceNodeId, e.TargetNodeId, e.RelationshipType, e.Strength, e.Criticality, e.Metadata));
        return new GraphSnapshot(organizationId, nodes, edges);
    }
}

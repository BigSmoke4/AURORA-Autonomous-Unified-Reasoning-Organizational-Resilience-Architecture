using Aurora.Modules.Graph.Domain;

namespace Aurora.Modules.Graph.Application;

/// <summary>
/// An in-memory, read-optimized projection of the graph for one organization (spec section 9:
/// "the graph visualization must NOT query PostgreSQL for every visual interaction"). Built once
/// by IGraphProjectionService and then reused for every traversal/algorithm call in a request.
/// </summary>
public sealed class GraphSnapshot
{
    public Guid OrganizationId { get; }
    public DateTime BuiltAtUtc { get; } = DateTime.UtcNow;

    private readonly Dictionary<Guid, GraphNode> _nodes;
    private readonly List<GraphEdge> _edges;
    private readonly Dictionary<Guid, List<GraphEdge>> _outgoing;
    private readonly Dictionary<Guid, List<GraphEdge>> _incoming;

    public IReadOnlyDictionary<Guid, GraphNode> Nodes => _nodes;
    public IReadOnlyList<GraphEdge> Edges => _edges;

    public GraphSnapshot(Guid organizationId, IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges)
    {
        OrganizationId = organizationId;
        _nodes = nodes.ToDictionary(n => n.Id);
        _edges = edges.ToList();

        _outgoing = _nodes.Keys.ToDictionary(id => id, _ => new List<GraphEdge>());
        _incoming = _nodes.Keys.ToDictionary(id => id, _ => new List<GraphEdge>());

        foreach (var edge in _edges)
        {
            if (_outgoing.TryGetValue(edge.SourceNodeId, out var outList)) outList.Add(edge);
            if (_incoming.TryGetValue(edge.TargetNodeId, out var inList)) inList.Add(edge);
        }
    }

    public IReadOnlyList<GraphEdge> OutgoingEdges(Guid nodeId) =>
        _outgoing.TryGetValue(nodeId, out var list) ? list : Array.Empty<GraphEdge>();

    public IReadOnlyList<GraphEdge> IncomingEdges(Guid nodeId) =>
        _incoming.TryGetValue(nodeId, out var list) ? list : Array.Empty<GraphEdge>();

    public GraphNode? Find(Guid nodeId) => _nodes.TryGetValue(nodeId, out var n) ? n : null;
}

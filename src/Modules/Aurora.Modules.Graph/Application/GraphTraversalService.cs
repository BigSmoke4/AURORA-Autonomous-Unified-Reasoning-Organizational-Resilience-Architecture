using Aurora.Modules.Graph.Domain;

namespace Aurora.Modules.Graph.Application;

public sealed record PathResult(bool Found, IReadOnlyList<Guid> NodeIds, decimal TotalWeight);

public sealed record ImpactedNode(Guid NodeId, int HopDistance, decimal PropagatedStrength);

/// <summary>Vertex minimum-cut for one dependent and one supplier type. CutSize is the number of suppliers that must fail.</summary>
public sealed record SupplyCut(Guid DependentNodeId, GraphNodeType SourceType, int CutSize, IReadOnlyList<Guid> CutNodeIds);

/// <summary>
/// The Graph Intelligence Engine (spec section 8). Pure, deterministic graph algorithms over a
/// GraphSnapshot — no I/O, no AI, fully unit-testable. This is the layer AI tools call into
/// (spec section 24: TraverseDependencies) rather than agents reasoning about the graph themselves.
/// </summary>
public sealed class GraphTraversalService
{
    /// <summary>Propagated signals below this strength are treated as negligible.</summary>
    public const decimal MinimumSignal = 0.02m;

    /// <summary>Breadth-first traversal outward along outgoing edges, returning visit order.</summary>
    public IReadOnlyList<Guid> BreadthFirstTraversal(GraphSnapshot snapshot, Guid startNodeId, int maxDepth = int.MaxValue)
    {
        var visited = new HashSet<Guid> { startNodeId };
        var order = new List<Guid> { startNodeId };
        var queue = new Queue<(Guid Id, int Depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            foreach (var edge in snapshot.OutgoingEdges(current))
            {
                if (visited.Add(edge.TargetNodeId))
                {
                    order.Add(edge.TargetNodeId);
                    queue.Enqueue((edge.TargetNodeId, depth + 1));
                }
            }
        }
        return order;
    }

    /// <summary>
    /// Shortest dependency path between two nodes (spec section 8). Uses Dijkstra with edge
    /// cost = (1 - Strength), so a "strong" dependency edge is cheaper to traverse than a weak
    /// one — the path AURORA reports is the one most likely to actually propagate an impact,
    /// not merely the fewest hops.
    /// </summary>
    public PathResult ShortestDependencyPath(GraphSnapshot snapshot, Guid sourceNodeId, Guid targetNodeId)
    {
        var distances = new Dictionary<Guid, decimal> { [sourceNodeId] = 0 };
        var previous = new Dictionary<Guid, Guid>();
        var visited = new HashSet<Guid>();
        var priorityQueue = new SortedSet<(decimal Dist, Guid Id)>(Comparer<(decimal, Guid)>.Create((a, b) =>
            a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2)));
        priorityQueue.Add((0, sourceNodeId));

        while (priorityQueue.Count > 0)
        {
            var (dist, current) = priorityQueue.Min;
            priorityQueue.Remove(priorityQueue.Min);
            if (!visited.Add(current)) continue;
            if (current == targetNodeId) break;

            foreach (var edge in snapshot.OutgoingEdges(current))
            {
                var edgeCost = 1.0m - edge.Strength + 0.01m; // never zero, keeps ordering stable
                var candidate = dist + edgeCost;
                if (!distances.TryGetValue(edge.TargetNodeId, out var existing) || candidate < existing)
                {
                    distances[edge.TargetNodeId] = candidate;
                    previous[edge.TargetNodeId] = current;
                    priorityQueue.Add((candidate, edge.TargetNodeId));
                }
            }
        }

        if (!distances.ContainsKey(targetNodeId))
            return new PathResult(false, Array.Empty<Guid>(), 0);

        var path = new List<Guid> { targetNodeId };
        var node = targetNodeId;
        while (previous.TryGetValue(node, out var prev))
        {
            path.Add(prev);
            node = prev;
        }
        path.Reverse();
        return new PathResult(true, path, distances[targetNodeId]);
    }

    /// <summary>
    /// Upstream analysis: everything this node depends on. An edge Source -&gt; Target means
    /// "Target depends on Source" (e.g. Supplier -SUPPLIES-&gt; Factory: the Factory depends on
    /// the Supplier), so walking upstream from a node means following its INCOMING edges back
    /// to their sources, recursively — what would need to be healthy for this node to function.
    /// </summary>
    public IReadOnlyList<ImpactedNode> UpstreamDependencies(GraphSnapshot snapshot, Guid nodeId, int maxDepth = 5)
        => Propagate(snapshot, nodeId, maxDepth, useOutgoing: false);

    /// <summary>
    /// Downstream impact analysis: everything that depends on this node. Following the same
    /// convention (Source -&gt; Target means Target depends on Source), walking downstream from a
    /// node means following its OUTGOING edges forward, recursively — what breaks if this node
    /// fails. This is the core call behind "What would be affected if X failed?" (spec section 8)
    /// and feeds the Simulation engine directly.
    /// </summary>
    public IReadOnlyList<ImpactedNode> DownstreamImpact(GraphSnapshot snapshot, Guid nodeId, int maxDepth = 8)
        => Propagate(snapshot, nodeId, maxDepth, useOutgoing: true);

    /// <summary>
    /// Max-product propagation. Each edge multiplies the signal by its strength (0..1], so a
    /// signal can only weaken along a path. A priority queue settles nodes in decreasing order
    /// of their best propagated strength, so each node reports the strongest path that reaches
    /// it (not merely the first path discovered). Paths longer than maxDepth hops are not
    /// expanded, and signals below MinimumSignal are dropped.
    /// </summary>
    private IReadOnlyList<ImpactedNode> Propagate(GraphSnapshot snapshot, Guid startNodeId, int maxDepth, bool useOutgoing)
    {
        var best = new Dictionary<Guid, (decimal Strength, int Hops)> { [startNodeId] = (1m, 0) };
        var frontier = new PriorityQueue<Guid, decimal>();
        frontier.Enqueue(startNodeId, -1m);

        while (frontier.TryDequeue(out var current, out var negativeStrength))
        {
            var (strength, hops) = best[current];
            if (-negativeStrength != strength) continue; // stale queue entry
            if (hops >= maxDepth) continue;

            var edges = useOutgoing ? snapshot.OutgoingEdges(current) : snapshot.IncomingEdges(current);
            foreach (var edge in edges)
            {
                var neighbor = useOutgoing ? edge.TargetNodeId : edge.SourceNodeId;
                if (neighbor == startNodeId) continue;

                var propagated = strength * edge.Strength;
                if (propagated < MinimumSignal) continue;

                if (!best.TryGetValue(neighbor, out var existing) || propagated > existing.Strength)
                {
                    best[neighbor] = (propagated, hops + 1);
                    frontier.Enqueue(neighbor, -propagated);
                }
            }
        }

        return best
            .Where(entry => entry.Key != startNodeId)
            .Select(entry => new ImpactedNode(entry.Key, entry.Value.Hops, entry.Value.Strength))
            .OrderByDescending(node => node.PropagatedStrength)
            .ThenBy(node => node.NodeId)
            .ToList();
    }

    /// <summary>
    /// Vertex minimum-cut of a dependent with respect to one supplier type: the fewest nodes of that type whose
    /// failure would cut the dependent off from every node of that type. CutNodeIds lists one minimum cut.
    /// A CutSize of 0 means no node of that type reaches the dependent.
    /// </summary>
    public SupplyCut MinimumSupplyCut(GraphSnapshot snapshot, Guid dependentNodeId, GraphNodeType sourceType)
    {
        var ids = OrderedNodeIds(snapshot);
        var index = IndexOf(ids);
        if (!index.TryGetValue(dependentNodeId, out var sinkIndex))
        {
            return new SupplyCut(dependentNodeId, sourceType, 0, Array.Empty<Guid>());
        }

        var network = BuildSupplyNetwork(snapshot, ids, index, sourceType);
        var source = SourceVertex(ids.Count);
        var flow = network.MaxFlow(source, InVertex(sinkIndex));

        var reachable = network.ResidualReachable(source);
        var cut = new List<Guid>();
        for (var i = 0; i < ids.Count; i++)
        {
            // A node is in the cut when its internal edge is saturated: its in-side is reachable and its out-side is not.
            if (snapshot.Nodes[ids[i]].NodeType == sourceType && reachable[InVertex(i)] && !reachable[OutVertex(i)])
            {
                cut.Add(ids[i]);
            }
        }

        return new SupplyCut(dependentNodeId, sourceType, flow, cut);
    }

    /// <summary>
    /// Every (dependent, supplier type) pair whose minimum cut has exactly one node. Removing that node alone
    /// disconnects the dependent from all suppliers of that type, so it is a single point of failure.
    /// </summary>
    public IReadOnlyList<SupplyCut> SinglePointsOfFailure(GraphSnapshot snapshot)
    {
        var ids = OrderedNodeIds(snapshot);
        var supplierTypes = ids.Select(id => snapshot.Nodes[id].NodeType).Distinct().OrderBy(t => t).ToList();
        var findings = new List<SupplyCut>();

        foreach (var sourceType in supplierTypes)
        {
            foreach (var dependentId in ids)
            {
                if (snapshot.Nodes[dependentId].NodeType == sourceType) continue;

                var cut = MinimumSupplyCut(snapshot, dependentId, sourceType);
                if (cut.CutSize == SingleNodeCut) findings.Add(cut);
            }
        }

        return findings;
    }

    /// <summary>
    /// Nodes that are a single point of failure for at least one dependent. Built on SinglePointsOfFailure, which
    /// uses vertex minimum-cut analysis rather than a local screen, so redundant suppliers are counted
    /// across multi-hop routes.
    /// </summary>
    public IReadOnlyList<Guid> DetectSingleSourcedNodes(GraphSnapshot snapshot) =>
        SinglePointsOfFailure(snapshot)
            .SelectMany(cut => cut.CutNodeIds)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

    private const int SingleNodeCut = 1;

    /// <summary>Capacity that stands for "cannot be removed" (structural edges and non-supplier nodes).</summary>
    private const int Unbounded = int.MaxValue / 4;

    private static List<Guid> OrderedNodeIds(GraphSnapshot snapshot) => snapshot.Nodes.Keys.OrderBy(id => id).ToList();

    private static Dictionary<Guid, int> IndexOf(IReadOnlyList<Guid> ids)
    {
        var index = new Dictionary<Guid, int>(ids.Count);
        for (var i = 0; i < ids.Count; i++) index[ids[i]] = i;
        return index;
    }

    private static int InVertex(int nodeIndex) => 2 * nodeIndex;

    private static int OutVertex(int nodeIndex) => 2 * nodeIndex + 1;

    private static int SourceVertex(int nodeCount) => 2 * nodeCount;

    /// <summary>
    /// Node-split flow network: each node becomes an in-vertex and an out-vertex joined by an edge of capacity 1 when
    /// the node is of the source type (removable supplier) and unbounded otherwise. Every supplier of the type is fed from
    /// a super source. Each dependency edge runs from the source's out-vertex to the target's in-vertex.
    /// </summary>
    private static FlowNetwork BuildSupplyNetwork(
        GraphSnapshot snapshot, IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, int> index, GraphNodeType sourceType)
    {
        var network = new FlowNetwork(2 * ids.Count + 1);
        var source = SourceVertex(ids.Count);

        for (var i = 0; i < ids.Count; i++)
        {
            var isSupplier = snapshot.Nodes[ids[i]].NodeType == sourceType;
            network.AddEdge(InVertex(i), OutVertex(i), isSupplier ? 1 : Unbounded);
            if (isSupplier) network.AddEdge(source, InVertex(i), Unbounded);
        }

        foreach (var edge in snapshot.Edges)
        {
            if (!index.TryGetValue(edge.SourceNodeId, out var from) || !index.TryGetValue(edge.TargetNodeId, out var to)) continue;
            network.AddEdge(OutVertex(from), InVertex(to), Unbounded);
        }

        return network;
    }
}

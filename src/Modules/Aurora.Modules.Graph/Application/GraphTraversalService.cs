namespace Aurora.Modules.Graph.Application;

public sealed record PathResult(bool Found, IReadOnlyList<Guid> NodeIds, decimal TotalWeight);

public sealed record ImpactedNode(Guid NodeId, int HopDistance, decimal PropagatedStrength);

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
    /// Single-sourcing screen. Node X is flagged when at least one dependent D (an edge X -&gt; D)
    /// has no other incoming supplier of X's node type. This is a local screen, not a minimum-cut
    /// analysis: it does not look past the immediate dependent for alternate paths, so it can
    /// over-report when a dependent has a longer backup route. Callers must present the result as
    /// "single-sourced nodes", not as a proven set of single points of failure.
    /// </summary>
    public IReadOnlyList<Guid> DetectSingleSourcedNodes(GraphSnapshot snapshot)
    {
        var singleSourced = new List<Guid>();
        foreach (var node in snapshot.Nodes.Values)
        {
            // Nodes that depend on 'node' are on the far end of node's OUTGOING edges
            // (an edge Supplier -> Factory means the Factory depends on the Supplier).
            var dependentEdges = snapshot.OutgoingEdges(node.Id);
            if (dependentEdges.Count == 0) continue;

            foreach (var dependentEdge in dependentEdges)
            {
                var dependent = dependentEdge.TargetNodeId;

                // Does the dependent have another incoming edge, from a different node of the
                // SAME type as 'node', that could stand in if 'node' failed?
                var alternateSuppliers = snapshot.IncomingEdges(dependent)
                    .Where(e => e.SourceNodeId != node.Id)
                    .Select(e => snapshot.Find(e.SourceNodeId))
                    .Count(n => n is not null && n.NodeType == node.NodeType);

                if (alternateSuppliers == 0)
                {
                    singleSourced.Add(node.Id);
                    break;
                }
            }
        }
        return singleSourced;
    }
}

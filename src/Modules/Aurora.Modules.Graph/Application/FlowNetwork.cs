namespace Aurora.Modules.Graph.Application;

/// <summary>
/// A small integer flow network with Edmonds-Karp (shortest augmenting path) max-flow. The graph module uses it
/// for vertex minimum-cut analysis, where node capacities are 1 and edge capacities are effectively unbounded.
/// Deterministic: vertices and edges are visited in insertion order.
/// </summary>
internal sealed class FlowNetwork
{
    private readonly List<int>[] _adjacency;
    private readonly List<int> _targets = new();
    private readonly List<int> _capacities = new();

    public FlowNetwork(int vertexCount)
    {
        _adjacency = new List<int>[vertexCount];
        for (var v = 0; v < vertexCount; v++) _adjacency[v] = new List<int>();
    }

    /// <summary>Adds a directed edge and its zero-capacity reverse. The reverse of edge e is always e XOR 1.</summary>
    public void AddEdge(int from, int to, int capacity)
    {
        _adjacency[from].Add(_targets.Count);
        _targets.Add(to);
        _capacities.Add(capacity);

        _adjacency[to].Add(_targets.Count);
        _targets.Add(from);
        _capacities.Add(0);
    }

    /// <summary>Computes the maximum flow from source to sink, leaving the residual capacities in place.</summary>
    public int MaxFlow(int source, int sink)
    {
        var total = 0;
        while (true)
        {
            var parentEdge = FindAugmentingPath(source, sink);
            if (parentEdge is null) return total;

            var bottleneck = int.MaxValue;
            for (var v = sink; v != source; v = _targets[parentEdge[v] ^ 1])
            {
                bottleneck = Math.Min(bottleneck, _capacities[parentEdge[v]]);
            }

            for (var v = sink; v != source; v = _targets[parentEdge[v] ^ 1])
            {
                _capacities[parentEdge[v]] -= bottleneck;
                _capacities[parentEdge[v] ^ 1] += bottleneck;
            }

            total += bottleneck;
        }
    }

    /// <summary>Vertices reachable from the source through edges that still have residual capacity.</summary>
    public bool[] ResidualReachable(int source)
    {
        var reachable = new bool[_adjacency.Length];
        var queue = new Queue<int>();
        reachable[source] = true;
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in _adjacency[current])
            {
                var next = _targets[edge];
                if (_capacities[edge] > 0 && !reachable[next])
                {
                    reachable[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return reachable;
    }

    private int[]? FindAugmentingPath(int source, int sink)
    {
        var parentEdge = new int[_adjacency.Length];
        Array.Fill(parentEdge, -1);
        var visited = new bool[_adjacency.Length];
        var queue = new Queue<int>();
        visited[source] = true;
        queue.Enqueue(source);

        while (queue.Count > 0 && !visited[sink])
        {
            var current = queue.Dequeue();
            foreach (var edge in _adjacency[current])
            {
                var next = _targets[edge];
                if (_capacities[edge] > 0 && !visited[next])
                {
                    visited[next] = true;
                    parentEdge[next] = edge;
                    queue.Enqueue(next);
                }
            }
        }

        return visited[sink] ? parentEdge : null;
    }
}

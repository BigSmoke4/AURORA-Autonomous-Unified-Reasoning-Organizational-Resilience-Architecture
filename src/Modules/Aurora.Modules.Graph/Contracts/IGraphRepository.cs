using Aurora.Modules.Graph.Domain;

namespace Aurora.Modules.Graph.Contracts;

/// <summary>Thin persistence contract implemented in Aurora.Web/Data against the composite
/// AuroraDbContext, injected into GraphProjectionService. Keeps the Application layer free of
/// any direct EF Core dependency on the Web project's DbContext.</summary>
public interface IGraphRepository
{
    Task<List<GraphNode>> GetNodesAsync(Guid organizationId, CancellationToken ct = default);
    Task<List<GraphEdge>> GetEdgesAsync(Guid organizationId, CancellationToken ct = default);
    Task AddNodeAsync(GraphNode node, CancellationToken ct = default);
    Task AddEdgeAsync(GraphEdge edge, CancellationToken ct = default);
    Task<GraphNode?> FindNodeByEntityAsync(Guid organizationId, GraphNodeType nodeType, Guid entityId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

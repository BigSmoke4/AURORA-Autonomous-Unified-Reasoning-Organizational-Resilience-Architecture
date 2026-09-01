using Aurora.Modules.Graph.Contracts;
using Aurora.Modules.Graph.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

public sealed class GraphRepository : IGraphRepository
{
    private readonly AuroraDbContext _db;
    public GraphRepository(AuroraDbContext db) => _db = db;

    public async Task<List<GraphNode>> GetNodesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.GraphNodes.AsNoTracking().Where(n => n.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<List<GraphEdge>> GetEdgesAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.GraphEdges.AsNoTracking().Where(e => e.OrganizationId == organizationId).ToListAsync(ct);

    public async Task AddNodeAsync(GraphNode node, CancellationToken ct = default) =>
        await _db.GraphNodes.AddAsync(node, ct);

    public async Task AddEdgeAsync(GraphEdge edge, CancellationToken ct = default) =>
        await _db.GraphEdges.AddAsync(edge, ct);

    public Task<GraphNode?> FindNodeByEntityAsync(Guid organizationId, GraphNodeType nodeType, Guid entityId, CancellationToken ct = default) =>
        _db.GraphNodes.FirstOrDefaultAsync(n =>
            n.OrganizationId == organizationId && n.NodeType == nodeType && n.EntityId == entityId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

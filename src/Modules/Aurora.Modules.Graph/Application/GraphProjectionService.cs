using Aurora.Modules.Graph.Contracts;

namespace Aurora.Modules.Graph.Application;

public sealed class GraphProjectionService : IGraphProjectionService
{
    private readonly IGraphRepository _repository;

    public GraphProjectionService(IGraphRepository repository) => _repository = repository;

    public async Task<GraphSnapshot> BuildLiveGraphAsync(Guid organizationId, CancellationToken ct = default)
    {
        var nodes = await _repository.GetNodesAsync(organizationId, ct);
        var edges = await _repository.GetEdgesAsync(organizationId, ct);
        return new GraphSnapshot(organizationId, nodes, edges);
    }
}

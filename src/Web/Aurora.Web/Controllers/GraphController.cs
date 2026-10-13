using Aurora.Modules.Graph.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Models;
using Aurora.Web.Models.Graph;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>
/// JSON API backing the neural graph visualization and graph intelligence queries (spec
/// section 9: "the graph visualization must NOT query PostgreSQL for every visual interaction").
/// Every action here builds one GraphSnapshot per request and runs pure in-memory algorithms
/// against it — no additional database round trips once the snapshot is projected.
/// </summary>
[Authorize(Policy = AuroraPermissions.GraphRead)]
[Route("api/graph")]
[ApiController]
public class GraphController : ControllerBase
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IGraphProjectionService _graphProjection;
    private readonly GraphTraversalService _traversal;

    public GraphController(CurrentOrganizationAccessor orgAccessor, IGraphProjectionService graphProjection, GraphTraversalService traversal)
    {
        _orgAccessor = orgAccessor;
        _graphProjection = graphProjection;
        _traversal = traversal;
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<GraphSnapshotDto>> Snapshot(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);

        var dto = new GraphSnapshotDto
        {
            Nodes = snapshot.Nodes.Values.Select(n => new GraphNodeDto
            {
                Id = n.Id.ToString(),
                Name = n.DisplayName,
                NodeType = n.NodeType.ToString(),
                Status = n.Status,
                Criticality = n.Criticality,
                RevenueExposure = n.RevenueExposure
            }).ToList(),
            Edges = snapshot.Edges.Select(e => new GraphEdgeDto
            {
                Id = e.Id.ToString(),
                Source = e.SourceNodeId.ToString(),
                Target = e.TargetNodeId.ToString(),
                RelationshipType = e.RelationshipType.ToString(),
                Strength = e.Strength
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpGet("downstream-impact/{nodeId:guid}")]
    public async Task<IActionResult> DownstreamImpact(Guid nodeId, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        if (snapshot.Find(nodeId) is null) return NotFound("Node not found in the current organization's graph.");

        var impacted = _traversal.DownstreamImpact(snapshot, nodeId);
        var result = impacted.Select(i => new
        {
            nodeId = i.NodeId,
            name = snapshot.Find(i.NodeId)?.DisplayName,
            hopDistance = i.HopDistance,
            propagatedStrength = i.PropagatedStrength,
            revenueExposure = snapshot.Find(i.NodeId)?.RevenueExposure
        });

        return Ok(result);
    }

    [HttpGet("single-sourced-nodes")]
    public async Task<IActionResult> SingleSourcedNodes(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var singleSourced = _traversal.DetectSingleSourcedNodes(snapshot);

        var result = singleSourced.Select(id => new
        {
            nodeId = id,
            name = snapshot.Find(id)?.DisplayName,
            nodeType = snapshot.Find(id)?.NodeType.ToString()
        });

        return Ok(result);
    }

    [HttpGet("shortest-path")]
    public async Task<IActionResult> ShortestPath([FromQuery] Guid sourceId, [FromQuery] Guid targetId, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var path = _traversal.ShortestDependencyPath(snapshot, sourceId, targetId);

        return Ok(new
        {
            found = path.Found,
            totalWeight = path.TotalWeight,
            nodes = path.NodeIds.Select(id => new { id, name = snapshot.Find(id)?.DisplayName })
        });
    }
}

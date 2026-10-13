using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Services;

/// <summary>
/// Captures the live digital twin graph and restores a captured one, so a counterfactual can run against the graph as it
/// was at the time of capture. The stored payload is the codec's output, so a restored graph is identical to the captured one.
/// Stages changes only; the caller commits with SaveChangesAsync.
/// </summary>
public sealed class GraphSnapshotStore(AuroraDbContext db, IGraphProjectionService projection, TimeProvider clock)
{
    public async Task<GraphSnapshotRecord> CaptureAsync(Guid organizationId, CancellationToken ct)
    {
        var live = await projection.BuildLiveGraphAsync(organizationId, ct);
        var record = new GraphSnapshotRecord(organizationId, clock.GetUtcNow().UtcDateTime, live.Nodes.Count, live.Edges.Count,
            GraphSnapshotCodec.Serialize(live));
        db.GraphSnapshotRecords.Add(record);
        return record;
    }

    /// <summary>Restores a captured graph. Returns null when no snapshot with that id belongs to the current tenant.</summary>
    public async Task<(GraphSnapshotRecord Record, GraphSnapshot Graph)?> RestoreAsync(Guid organizationId, Guid snapshotId, CancellationToken ct)
    {
        var record = await db.GraphSnapshotRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == snapshotId, ct);
        return record is null ? null : (record, GraphSnapshotCodec.Deserialize(organizationId, record.PayloadJson));
    }

    /// <summary>The most recent captures for the current tenant, newest first.</summary>
    public async Task<IReadOnlyList<GraphSnapshotRecord>> ListRecentAsync(int limit, CancellationToken ct) =>
        await db.GraphSnapshotRecords.AsNoTracking().OrderByDescending(r => r.CapturedAtUtc).Take(limit).ToListAsync(ct);
}

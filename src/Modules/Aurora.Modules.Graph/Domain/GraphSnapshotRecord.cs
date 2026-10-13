using Aurora.Shared.Kernel;

namespace Aurora.Modules.Graph.Domain;

/// <summary>
/// A captured state of an organization's graph, kept so that counterfactuals can run against the graph as it was
/// at a given time. The payload is the JSON written by GraphSnapshotCodec.
/// </summary>
public class GraphSnapshotRecord : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public DateTime CapturedAtUtc { get; private set; }
    public int NodeCount { get; private set; }
    public int EdgeCount { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;

    protected GraphSnapshotRecord()
    {
    }

    public GraphSnapshotRecord(Guid organizationId, DateTime capturedAtUtc, int nodeCount, int edgeCount, string payloadJson)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        CapturedAtUtc = DomainGuard.ToUtc(capturedAtUtc);
        NodeCount = DomainGuard.RequireRange(nodeCount, 0, int.MaxValue, nameof(nodeCount));
        EdgeCount = DomainGuard.RequireRange(edgeCount, 0, int.MaxValue, nameof(edgeCount));
        PayloadJson = DomainGuard.RequireText(payloadJson, int.MaxValue, nameof(payloadJson));
    }
}

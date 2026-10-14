using Aurora.Shared.Kernel;

namespace Aurora.Modules.Graph.Domain;

public class GraphEdge : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid SourceNodeId { get; private set; }
    public Guid TargetNodeId { get; private set; }
    public GraphRelationshipType RelationshipType { get; private set; }

    /// <summary>0.0-1.0. How strongly an event at the source propagates to the target.
    /// Used directly as the decay/attenuation factor in impact propagation.</summary>
    public decimal Strength { get; private set; } = 0.5m;

    public int Criticality { get; private set; } = 3;
    public string? Metadata { get; private set; }

    protected GraphEdge() { }

    /// <summary>Rebuilds an edge from a stored snapshot. The original id is kept.</summary>
    public static GraphEdge Restore(Guid id, Guid organizationId, Guid sourceNodeId, Guid targetNodeId,
        GraphRelationshipType relationshipType, decimal strength, int criticality, string? metadata) =>
        new(organizationId, sourceNodeId, targetNodeId, relationshipType, strength, criticality, metadata) { Id = id };

    public GraphEdge(Guid organizationId, Guid sourceNodeId, Guid targetNodeId,
        GraphRelationshipType relationshipType, decimal strength, int criticality, string? metadata = null)
    {
        OrganizationId = organizationId;
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        RelationshipType = relationshipType;
        Strength = Math.Clamp(strength, 0m, 1m);
        Criticality = Math.Clamp(criticality, 1, 5);
        Metadata = metadata;
    }
}

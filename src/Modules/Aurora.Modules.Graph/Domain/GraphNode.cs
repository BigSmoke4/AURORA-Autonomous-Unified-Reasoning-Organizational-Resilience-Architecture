using Aurora.Shared.Kernel;

namespace Aurora.Modules.Graph.Domain;

/// <summary>
/// A projected node in the enterprise graph. GraphNode does not duplicate business data —
/// EntityId points back at the owning row in DigitalTwin (or another module); GraphNode only
/// carries what the graph engine and visualization need: identity, type, display, and status.
/// </summary>
public class GraphNode : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public GraphNodeType NodeType { get; private set; }
    public Guid EntityId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Healthy";

    /// <summary>1 (low) - 5 (critical). Drives node size in the neural graph visualization
    /// (spec section 55).</summary>
    public int Criticality { get; private set; } = 3;

    /// <summary>Annual revenue exposure attached to this node, copied from the owning
    /// DigitalTwin entity at projection time. This is the deterministic basis the Simulation
    /// engine uses for financial-impact math (spec section 74) — never invented at run time.</summary>
    public decimal RevenueExposure { get; private set; }

    protected GraphNode() { }

    public GraphNode(Guid organizationId, GraphNodeType nodeType, Guid entityId, string displayName,
        int criticality, decimal revenueExposure, string status = "Healthy")
    {
        OrganizationId = organizationId;
        NodeType = nodeType;
        EntityId = entityId;
        DisplayName = displayName;
        Criticality = Math.Clamp(criticality, 1, 5);
        RevenueExposure = revenueExposure;
        Status = status;
    }
}

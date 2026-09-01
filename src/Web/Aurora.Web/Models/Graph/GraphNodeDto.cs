namespace Aurora.Web.Models.Graph;

/// <summary>JSON shape sent to aurora.graph.js for the neural graph visualization.</summary>
public class GraphNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Criticality { get; set; }
    public decimal RevenueExposure { get; set; }
}

public class GraphEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public decimal Strength { get; set; }
}

public class GraphSnapshotDto
{
    public List<GraphNodeDto> Nodes { get; set; } = new();
    public List<GraphEdgeDto> Edges { get; set; } = new();
}

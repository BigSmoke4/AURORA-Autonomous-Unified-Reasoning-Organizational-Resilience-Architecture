namespace Aurora.Modules.DigitalTwin.Domain;

/// <summary>Shared health status vocabulary used across every digital-twin entity and mirrored
/// onto the corresponding GraphNode (spec section 55: node status = Healthy/Warning/Critical/Unknown).</summary>
public enum OperationalStatus
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Critical = 3
}

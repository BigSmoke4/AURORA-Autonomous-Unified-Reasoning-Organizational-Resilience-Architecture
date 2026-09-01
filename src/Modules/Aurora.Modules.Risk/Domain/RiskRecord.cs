using Aurora.Shared.Kernel;
using Aurora.Modules.Risk.Application;

namespace Aurora.Modules.Risk.Domain;

/// <summary>A scored risk against a specific graph node (spec section 18).</summary>
public class RiskRecord : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid NodeId { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>0.0-1.0</summary>
    public decimal Probability { get; private set; }
    /// <summary>0.0-1.0</summary>
    public decimal Impact { get; private set; }
    /// <summary>0.0-1.0 — how exposed the organization is if this risk materializes.</summary>
    public decimal Exposure { get; private set; }
    /// <summary>1-5, taken from the node's Criticality.</summary>
    public int Criticality { get; private set; }

    public decimal Score { get; private set; }

    protected RiskRecord() { }

    public RiskRecord(Guid organizationId, Guid nodeId, string title, decimal probability, decimal impact,
        decimal exposure, int criticality, RiskScoringWeights weights)
    {
        OrganizationId = organizationId;
        NodeId = nodeId;
        Title = title;
        Probability = Math.Clamp(probability, 0, 1);
        Impact = Math.Clamp(impact, 0, 1);
        Exposure = Math.Clamp(exposure, 0, 1);
        Criticality = Math.Clamp(criticality, 1, 5);
        Score = RiskScoringService.Score(Probability, Impact, Exposure, Criticality, weights);
    }
}

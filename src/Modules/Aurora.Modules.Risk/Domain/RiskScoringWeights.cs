namespace Aurora.Modules.Risk.Domain;

/// <summary>
/// Configurable weights for the risk formula (spec section 18: "the exact formula should be
/// configurable"). Bound from appsettings.json ("Risk:ScoringWeights") — see Aurora.Web/Program.cs.
/// </summary>
public sealed class RiskScoringWeights
{
    public decimal ProbabilityWeight { get; set; } = 1.0m;
    public decimal ImpactWeight { get; set; } = 1.0m;
    public decimal ExposureWeight { get; set; } = 1.0m;
    public decimal CriticalityWeight { get; set; } = 1.0m;
}

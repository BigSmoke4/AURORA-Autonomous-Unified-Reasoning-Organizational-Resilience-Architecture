namespace Aurora.Modules.Decisions.Domain;

/// <summary>Spec section 84 default weights — organization-configurable via appsettings
/// ("Decisions:ScoringWeights"), never hard-coded globally.</summary>
public sealed class DecisionScoringWeights
{
    public decimal FinancialBenefitWeight { get; set; } = 0.30m;
    public decimal RiskReductionWeight { get; set; } = 0.25m;
    public decimal OperationalFeasibilityWeight { get; set; } = 0.20m;
    public decimal StrategicAlignmentWeight { get; set; } = 0.15m;
    public decimal TimeToBenefitWeight { get; set; } = 0.10m;
}

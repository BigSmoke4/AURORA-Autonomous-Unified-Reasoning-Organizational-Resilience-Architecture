using Aurora.Modules.Decisions.Domain;

namespace Aurora.Modules.Decisions.Application;

public sealed record DecisionFactorScores(
    decimal FinancialBenefit, decimal RiskReduction, decimal OperationalFeasibility,
    decimal StrategicAlignment, decimal TimeToBenefit);

/// <summary>Weighted-sum decision scoring (spec section 84). Each factor is scored 0-100 by the
/// caller (typically derived from simulation/risk output) before being combined here.</summary>
public static class DecisionScoringService
{
    public static decimal Score(DecisionFactorScores factors, DecisionScoringWeights weights)
    {
        var weighted =
            factors.FinancialBenefit * weights.FinancialBenefitWeight +
            factors.RiskReduction * weights.RiskReductionWeight +
            factors.OperationalFeasibility * weights.OperationalFeasibilityWeight +
            factors.StrategicAlignment * weights.StrategicAlignmentWeight +
            factors.TimeToBenefit * weights.TimeToBenefitWeight;

        return Math.Round(weighted, 2);
    }
}

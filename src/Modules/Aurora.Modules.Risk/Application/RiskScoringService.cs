using Aurora.Modules.Risk.Domain;

namespace Aurora.Modules.Risk.Application;

/// <summary>
/// Deterministic risk scoring (spec section 18). Score = (Probability^w1) x (Impact^w2) x
/// (Exposure^w3) x (Criticality/5)^w4, normalized to a 0-100 scale. Weights default to 1.0
/// (a plain product) and are configurable per organization without a code change.
/// </summary>
public static class RiskScoringService
{
    public static decimal Score(decimal probability, decimal impact, decimal exposure, int criticality, RiskScoringWeights weights)
    {
        var normalizedCriticality = criticality / 5m;

        double p = Math.Pow((double)probability, (double)weights.ProbabilityWeight);
        double i = Math.Pow((double)impact, (double)weights.ImpactWeight);
        double e = Math.Pow((double)exposure, (double)weights.ExposureWeight);
        double c = Math.Pow((double)normalizedCriticality, (double)weights.CriticalityWeight);

        var raw = p * i * e * c;
        return Math.Round((decimal)raw * 100m, 2);
    }
}

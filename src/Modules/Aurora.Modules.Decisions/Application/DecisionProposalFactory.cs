using System.Text.Json;
using Aurora.Modules.Decisions.Domain;

namespace Aurora.Modules.Decisions.Application;

/// <summary>Inputs from a counterfactual analysis, already read from the stored, versioned result.</summary>
public sealed record CounterfactualEvidence(
    Guid AnalysisRecordId,
    string ModelVersion,
    string TargetName,
    decimal BaselineRevenueImpact,
    decimal RevenueImpactAvoided,
    decimal PeakImpactReductionPct);

/// <summary>Human inputs captured on the proposal form.</summary>
public sealed record ProposalJudgements(
    decimal OperationalFeasibility,
    decimal StrategicAlignment,
    int DaysToBenefit,
    decimal Confidence);

/// <summary>
/// Converts counterfactual evidence into a scored, pending decision. Every factor is derived by a
/// stated formula from the analysis numbers or from a human judgement that is stored with the
/// decision. The factor values are persisted as JSON, so the score can be audited later.
/// </summary>
public static class DecisionProposalFactory
{
    public const int MaxBenefitDays = 180;
    public const decimal MaxScore = 100m;

    public static Decision FromCounterfactual(
        Guid organizationId,
        Guid proposedByUserId,
        CounterfactualEvidence evidence,
        ProposalJudgements judgements,
        DecisionScoringWeights weights)
    {
        ValidateJudgements(judgements);

        var financialBenefit = evidence.BaselineRevenueImpact > 0m
            ? Clamp(evidence.RevenueImpactAvoided / evidence.BaselineRevenueImpact * MaxScore)
            : 0m;
        var riskReduction = Clamp(evidence.PeakImpactReductionPct);
        var timeToBenefit = Clamp(MaxScore - judgements.DaysToBenefit * MaxScore / MaxBenefitDays);

        var factors = new DecisionFactorScores(
            Math.Round(financialBenefit, 2),
            Math.Round(riskReduction, 2),
            Math.Round(Clamp(judgements.OperationalFeasibility), 2),
            Math.Round(Clamp(judgements.StrategicAlignment), 2),
            Math.Round(timeToBenefit, 2));

        var score = DecisionScoringService.Score(factors, weights);

        var recommendation =
            $"Diversify supply for {evidence.TargetName} so that about {evidence.RevenueImpactAvoided:N0} " +
            $"of simulated revenue impact is avoided, reducing the peak daily impact by {evidence.PeakImpactReductionPct:0.##}%.";

        var evidenceSummary =
            $"Counterfactual analysis {evidence.AnalysisRecordId} (model {evidence.ModelVersion}): baseline revenue impact " +
            $"{evidence.BaselineRevenueImpact:N2}, avoided {evidence.RevenueImpactAvoided:N2}.";

        var factorsJson = JsonSerializer.Serialize(new
        {
            factors.FinancialBenefit,
            factors.RiskReduction,
            factors.OperationalFeasibility,
            factors.StrategicAlignment,
            factors.TimeToBenefit,
            Weights = weights,
            judgements.DaysToBenefit,
        });

        return new Decision(new DecisionDraft(
            organizationId,
            Truncate($"Mitigate {evidence.TargetName} single-source exposure", Decision.MaxTitleLength),
            Truncate(recommendation, Decision.MaxRecommendationLength),
            Truncate(evidenceSummary, Decision.MaxEvidenceLength),
            score,
            Math.Round(judgements.Confidence, 2),
            DecisionSourceKind.Counterfactual,
            evidence.AnalysisRecordId,
            proposedByUserId,
            evidence.ModelVersion,
            evidence.RevenueImpactAvoided,
            factorsJson));
    }

    private static void ValidateJudgements(ProposalJudgements judgements)
    {
        if (judgements.DaysToBenefit < 0) throw new ArgumentOutOfRangeException(nameof(judgements), "Days to benefit cannot be negative.");
        if (judgements.OperationalFeasibility is < 0m or > MaxScore) throw new ArgumentOutOfRangeException(nameof(judgements), "Feasibility must be 0-100.");
        if (judgements.StrategicAlignment is < 0m or > MaxScore) throw new ArgumentOutOfRangeException(nameof(judgements), "Alignment must be 0-100.");
        if (judgements.Confidence is < 0m or > MaxScore) throw new ArgumentOutOfRangeException(nameof(judgements), "Confidence must be 0-100.");
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, MaxScore);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

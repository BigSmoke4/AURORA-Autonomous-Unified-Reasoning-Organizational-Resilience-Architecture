using System.Globalization;
using Aurora.Modules.AI.Domain;

namespace Aurora.Modules.AI.Application;

/// <summary>What the debate needs from a decision. All values are the decision's recorded numbers.</summary>
public sealed record DebateCase(
    decimal PredictedRevenueImpact,
    decimal Confidence,
    decimal DecisionScore,
    decimal? PredictionErrorPct,
    bool IsCounterfactualBacked);

public sealed record DebateArgumentDraft(DebateRole Role, bool Supports, string Claim);

public sealed record DebateOutcome(
    IReadOnlyList<DebateArgumentDraft> Arguments,
    int AdvocatePoints,
    int CriticPoints,
    string Verdict,
    string Rationale);

/// <summary>
/// Runs the advocate and the critic over the same numbers and lets a count-based judge decide. The advocate counts the
/// criteria a decision meets. The critic counts the objections it raises. The verdict is "Recommend approval" only when
/// the advocate's count is strictly higher. A tie goes to "Revise before approval", the cautious outcome.
/// This is rule-based reasoning over stored figures. It involves no language model, and every claim states its threshold.
/// </summary>
public static class DecisionDebateEngine
{
    public const decimal MinimumDecisionScore = 60m;
    public const decimal MinimumConfidence = 70m;
    public const decimal MaximumPredictionErrorPct = 25m;
    public const string ApprovalVerdict = "Recommend approval";
    public const string RevisionVerdict = "Revise before approval";

    public static DebateOutcome Run(DebateCase input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var culture = CultureInfo.InvariantCulture;

        var advocate = new List<DebateArgumentDraft>
        {
            new(DebateRole.Advocate, input.PredictedRevenueImpact > 0m,
                input.PredictedRevenueImpact > 0m
                    ? $"Predicted revenue impact of {input.PredictedRevenueImpact.ToString("N2", culture)} is positive."
                    : "Predicted revenue impact is not positive."),
            new(DebateRole.Advocate, input.DecisionScore >= MinimumDecisionScore,
                input.DecisionScore >= MinimumDecisionScore
                    ? $"Decision score {input.DecisionScore.ToString("0.00", culture)} meets the {MinimumDecisionScore.ToString("0", culture)} threshold."
                    : $"Decision score {input.DecisionScore.ToString("0.00", culture)} is below the {MinimumDecisionScore.ToString("0", culture)} threshold."),
            new(DebateRole.Advocate, input.Confidence >= MinimumConfidence,
                input.Confidence >= MinimumConfidence
                    ? $"Model confidence {input.Confidence.ToString("0.00", culture)}% meets the {MinimumConfidence.ToString("0", culture)}% threshold."
                    : $"Model confidence {input.Confidence.ToString("0.00", culture)}% is below the {MinimumConfidence.ToString("0", culture)}% threshold."),
            new(DebateRole.Advocate, input.IsCounterfactualBacked,
                input.IsCounterfactualBacked
                    ? "The recommendation comes from a counterfactual analysis of the graph."
                    : "The recommendation was not produced by a counterfactual analysis."),
        };

        var critic = new List<DebateArgumentDraft>
        {
            Objection(input.Confidence < MinimumConfidence,
                $"Model confidence {input.Confidence.ToString("0.00", culture)}% is below the {MinimumConfidence.ToString("0", culture)}% threshold.",
                "Model confidence clears its threshold."),
            Objection(input.DecisionScore < MinimumDecisionScore,
                $"Decision score {input.DecisionScore.ToString("0.00", culture)} is below the {MinimumDecisionScore.ToString("0", culture)} threshold.",
                "Decision score clears its threshold."),
            Objection(input.PredictedRevenueImpact <= 0m,
                "Predicted revenue impact is not positive.",
                "Predicted revenue impact is positive."),
            Objection(input.PredictionErrorPct is { } error && Math.Abs(error) > MaximumPredictionErrorPct,
                $"Recorded prediction error of {(input.PredictionErrorPct ?? 0m).ToString("0.00", culture)}% exceeds the {MaximumPredictionErrorPct.ToString("0", culture)}% tolerance.",
                input.PredictionErrorPct is null
                    ? "No outcome has been recorded, so there is no prediction error to test."
                    : "Recorded prediction error is within tolerance."),
            Objection(!input.IsCounterfactualBacked,
                "No counterfactual analysis backs this recommendation.",
                "A counterfactual analysis backs this recommendation."),
        };

        var advocatePoints = advocate.Count(a => a.Supports);
        var criticPoints = critic.Count(c => !c.Supports);
        var verdict = advocatePoints > criticPoints ? ApprovalVerdict : RevisionVerdict;
        var rationale = $"{advocatePoints} of {advocate.Count} supporting criteria met; {criticPoints} of {critic.Count} objections raised. " +
            (verdict == ApprovalVerdict
                ? "Supporting criteria outnumber objections."
                : "Objections are not outnumbered, so the decision needs revision before approval.");

        var judge = new DebateArgumentDraft(DebateRole.Judge, verdict == ApprovalVerdict, rationale);
        return new DebateOutcome([.. advocate, .. critic, judge], advocatePoints, criticPoints, verdict, rationale);
    }

    private static DebateArgumentDraft Objection(bool raised, string objection, string noObjection) =>
        raised
            ? new DebateArgumentDraft(DebateRole.Critic, false, objection)
            : new DebateArgumentDraft(DebateRole.Critic, true, noObjection);
}

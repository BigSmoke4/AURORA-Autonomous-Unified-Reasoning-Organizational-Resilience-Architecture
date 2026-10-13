using Aurora.Shared.Kernel;

namespace Aurora.Modules.Decisions.Domain;

public enum DecisionSourceKind { Manual = 0, Counterfactual = 1 }

/// <summary>Everything needed to create a proposal. Built by the Application layer from evidence.</summary>
public sealed record DecisionDraft(
    Guid OrganizationId,
    string Title,
    string Recommendation,
    string EvidenceSummary,
    decimal DecisionScore,
    decimal Confidence,
    DecisionSourceKind SourceKind,
    Guid? SourceAnalysisId,
    Guid ProposedByUserId,
    string ModelVersion,
    decimal PredictedRevenueImpact,
    string ScoringFactorsJson);

/// <summary>
/// A recommendation that requires human sign-off before it is acted on (spec sections 31-32).
/// The transitions are: pending -> approved | rejected; approved -> execution started -> outcome
/// recorded. AURORA never executes a decision itself. Each transition is a separate, audited user
/// action that needs its own permission.
/// </summary>
public class Decision : AggregateRoot, ITenantScoped
{
    public const int MaxTitleLength = 300;
    public const int MaxRecommendationLength = 2000;
    public const int MaxEvidenceLength = 4000;
    private const int PercentDecimals = 2;

    public Guid OrganizationId { get; private set; }
    public Guid? SimulationRunId { get; private set; }
    public Guid? SourceAnalysisId { get; private set; }
    public DecisionSourceKind SourceKind { get; private set; } = DecisionSourceKind.Manual;
    public string Title { get; private set; } = string.Empty;
    public string Recommendation { get; private set; } = string.Empty;
    public string? EvidenceSummary { get; private set; }

    /// <summary>0-100, from DecisionScoringService (spec section 84).</summary>
    public decimal DecisionScore { get; private set; }

    /// <summary>0-100. Proposer's judgement of how well the evidence supports the recommendation.</summary>
    public decimal Confidence { get; private set; }

    public DecisionApprovalState ApprovalState { get; private set; } = DecisionApprovalState.PendingApproval;
    public DecisionExecutionState ExecutionState { get; private set; } = DecisionExecutionState.NotStarted;
    public Guid ProposedByUserId { get; private set; }
    public Guid? ApproverId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }

    public string ModelVersion { get; private set; } = string.Empty;
    public decimal PredictedRevenueImpact { get; private set; }
    public decimal? ActualRevenueImpact { get; private set; }

    /// <summary>(actual - predicted) / |predicted| * 100. Null until an outcome is recorded, or when the prediction was zero.</summary>
    public decimal? PredictionErrorPct { get; private set; }

    public string ScoringFactorsJson { get; private set; } = "{}";
    public string? PredictedOutcomeSummary { get; private set; }
    public string? ActualOutcomeSummary { get; private set; }

    protected Decision() { }

    public Decision(DecisionDraft draft)
    {
        OrganizationId = draft.OrganizationId;
        Title = draft.Title;
        Recommendation = draft.Recommendation;
        EvidenceSummary = draft.EvidenceSummary;
        DecisionScore = draft.DecisionScore;
        Confidence = draft.Confidence;
        SourceKind = draft.SourceKind;
        SourceAnalysisId = draft.SourceAnalysisId;
        ProposedByUserId = draft.ProposedByUserId;
        ModelVersion = draft.ModelVersion;
        PredictedRevenueImpact = draft.PredictedRevenueImpact;
        ScoringFactorsJson = draft.ScoringFactorsJson;
        PredictedOutcomeSummary = $"Predicted revenue impact avoided: {draft.PredictedRevenueImpact:N2}";
    }

    public void Approve(Guid approverId)
    {
        EnsureIndependentApprover(approverId);
        if (ApprovalState != DecisionApprovalState.PendingApproval)
            throw new InvalidOperationException("Only a pending decision can be approved.");
        ApprovalState = DecisionApprovalState.Approved;
        ApproverId = approverId;
        ApprovedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Reject(Guid approverId)
    {
        EnsureIndependentApprover(approverId);
        if (ApprovalState != DecisionApprovalState.PendingApproval)
            throw new InvalidOperationException("Only a pending decision can be rejected.");
        ApprovalState = DecisionApprovalState.Rejected;
        ApproverId = approverId;
        ApprovedAtUtc = DateTime.UtcNow;
        Touch();
    }

    /// <summary>Separation of duties: the person who proposed a decision cannot approve or reject it.</summary>
    private void EnsureIndependentApprover(Guid approverId)
    {
        if (approverId == ProposedByUserId)
            throw new InvalidOperationException("The proposer of a decision cannot approve or reject it.");
    }

    public void StartExecution()
    {
        if (ApprovalState != DecisionApprovalState.Approved)
            throw new InvalidOperationException("Only an approved decision can be executed.");
        if (ExecutionState != DecisionExecutionState.NotStarted)
            throw new InvalidOperationException("Execution has already started for this decision.");
        ExecutionState = DecisionExecutionState.InProgress;
        Touch();
    }

    /// <summary>Records what actually happened and computes the prediction error against the forecast.</summary>
    public void RecordActualOutcome(decimal actualRevenueImpact, string actualOutcomeSummary)
    {
        if (ApprovalState != DecisionApprovalState.Approved)
            throw new InvalidOperationException("Only an approved decision can record an outcome.");
        if (ExecutionState == DecisionExecutionState.Executed)
            throw new InvalidOperationException("An outcome has already been recorded for this decision.");

        ActualRevenueImpact = actualRevenueImpact;
        PredictionErrorPct = PredictedRevenueImpact == 0m
            ? null
            : Math.Round((actualRevenueImpact - PredictedRevenueImpact) / Math.Abs(PredictedRevenueImpact) * 100m, PercentDecimals);
        ActualOutcomeSummary = actualOutcomeSummary;
        ExecutionState = DecisionExecutionState.Executed;
        Touch();
    }
}

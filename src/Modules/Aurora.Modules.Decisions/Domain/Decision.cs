using Aurora.Shared.Kernel;

namespace Aurora.Modules.Decisions.Domain;

/// <summary>
/// A recommendation that requires human sign-off before it is acted on (spec section 31/32:
/// human-in-the-loop). AURORA never auto-executes a high-impact recommendation — Approve is
/// always a distinct, audited user action performed by someone holding Decision.Approve.
/// </summary>
public class Decision : AggregateRoot, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid? SimulationRunId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Recommendation { get; private set; } = string.Empty;
    public string? EvidenceSummary { get; private set; }

    /// <summary>0-100, from DecisionScoringService (spec section 84).</summary>
    public decimal DecisionScore { get; private set; }

    /// <summary>0-100. How confident the recommendation is, from the evidence behind it.</summary>
    public decimal Confidence { get; private set; }

    public DecisionApprovalState ApprovalState { get; private set; } = DecisionApprovalState.PendingApproval;
    public DecisionExecutionState ExecutionState { get; private set; } = DecisionExecutionState.NotStarted;
    public Guid? ApproverId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? PredictedOutcomeSummary { get; private set; }
    public string? ActualOutcomeSummary { get; private set; }

    protected Decision() { }

    public Decision(Guid organizationId, string title, string recommendation, decimal decisionScore,
        decimal confidence, string? evidenceSummary = null, Guid? simulationRunId = null,
        string? predictedOutcomeSummary = null)
    {
        OrganizationId = organizationId;
        Title = title;
        Recommendation = recommendation;
        DecisionScore = decisionScore;
        Confidence = confidence;
        EvidenceSummary = evidenceSummary;
        SimulationRunId = simulationRunId;
        PredictedOutcomeSummary = predictedOutcomeSummary;
    }

    public void Approve(Guid approverId)
    {
        if (ApprovalState != DecisionApprovalState.PendingApproval)
            throw new InvalidOperationException("Only a pending decision can be approved.");
        ApprovalState = DecisionApprovalState.Approved;
        ApproverId = approverId;
        ApprovedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Reject(Guid approverId)
    {
        if (ApprovalState != DecisionApprovalState.PendingApproval)
            throw new InvalidOperationException("Only a pending decision can be rejected.");
        ApprovalState = DecisionApprovalState.Rejected;
        ApproverId = approverId;
        ApprovedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void RecordActualOutcome(string actualOutcomeSummary)
    {
        ActualOutcomeSummary = actualOutcomeSummary;
        ExecutionState = DecisionExecutionState.Executed;
        Touch();
    }
}

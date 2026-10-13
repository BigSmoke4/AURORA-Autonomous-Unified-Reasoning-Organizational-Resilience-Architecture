using Aurora.Modules.Decisions.Application;
using Aurora.Modules.Decisions.Domain;
using Xunit;

namespace Aurora.Tests;

public class DecisionProposalFactoryTests
{
    private readonly Guid _org = Guid.NewGuid();
    private readonly Guid _proposer = Guid.NewGuid();

    private static CounterfactualEvidence Evidence(decimal baseline, decimal avoided, decimal peakReduction) =>
        new(Guid.NewGuid(), "target-edge-attenuation-v1", "Sole supplier", baseline, avoided, peakReduction);

    [Fact]
    public void Score_IsTheWeightedSumOfTheDocumentedFactors()
    {
        // Financial benefit = 250,000 / 1,000,000 * 100 = 25.
        // Risk reduction = 40. Feasibility = 80. Alignment = 60.
        // Time to benefit = 100 - 90 * 100 / 180 = 50.
        // Score = 25*0.30 + 40*0.25 + 80*0.20 + 60*0.15 + 50*0.10 = 47.50.
        var judgements = new ProposalJudgements(80m, 60m, 90, 50m);

        var decision = DecisionProposalFactory.FromCounterfactual(_org, _proposer,
            Evidence(1_000_000m, 250_000m, 40m), judgements, new DecisionScoringWeights());

        Assert.Equal(47.50m, decision.DecisionScore);
        Assert.Equal(DecisionSourceKind.Counterfactual, decision.SourceKind);
        Assert.Equal(DecisionApprovalState.PendingApproval, decision.ApprovalState);
        Assert.Equal(250_000m, decision.PredictedRevenueImpact);
    }

    [Fact]
    public void ZeroBaseline_GivesZeroFinancialBenefit_WithoutDividingByZero()
    {
        var decision = DecisionProposalFactory.FromCounterfactual(_org, _proposer,
            Evidence(0m, 0m, 0m), new ProposalJudgements(0m, 0m, 180, 0m), new DecisionScoringWeights());

        Assert.Equal(0m, decision.DecisionScore);
    }

    [Fact]
    public void NegativeDaysToBenefit_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DecisionProposalFactory.FromCounterfactual(_org, _proposer,
            Evidence(1m, 1m, 1m), new ProposalJudgements(50m, 50m, -1, 50m), new DecisionScoringWeights()));
    }

    [Fact]
    public void Proposer_CannotApproveTheirOwnDecision()
    {
        var decision = CreateDecision();

        Assert.Throws<InvalidOperationException>(() => decision.Approve(_proposer));
        Assert.Throws<InvalidOperationException>(() => decision.Reject(_proposer));
        Assert.Equal(DecisionApprovalState.PendingApproval, decision.ApprovalState);
    }

    [Fact]
    public void AnotherPerson_CanApprove_ThenExecutionAndOutcomeFollowInOrder()
    {
        var decision = CreateDecision();
        var approver = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => decision.StartExecution());

        decision.Approve(approver);
        decision.StartExecution();
        decision.RecordActualOutcome(200_000m, "Partial diversification held.");

        Assert.Equal(DecisionApprovalState.Approved, decision.ApprovalState);
        Assert.Equal(approver, decision.ApproverId);
        Assert.Equal(DecisionExecutionState.Executed, decision.ExecutionState);
        // (200,000 - 250,000) / 250,000 * 100 = -20.00
        Assert.Equal(-20.00m, decision.PredictionErrorPct);
    }

    [Fact]
    public void Outcome_CannotBeRecordedTwice()
    {
        var decision = CreateDecision();
        decision.Approve(Guid.NewGuid());
        decision.RecordActualOutcome(100m, "First outcome.");

        Assert.Throws<InvalidOperationException>(() => decision.RecordActualOutcome(120m, "Second outcome."));
    }

    [Fact]
    public void PredictionError_IsNull_WhenPredictionWasZero()
    {
        var decision = DecisionProposalFactory.FromCounterfactual(_org, _proposer,
            Evidence(1m, 0m, 0m), new ProposalJudgements(50m, 50m, 90, 50m), new DecisionScoringWeights());
        decision.Approve(Guid.NewGuid());

        decision.RecordActualOutcome(10m, "Observed impact.");

        Assert.Null(decision.PredictionErrorPct);
    }

    private Decision CreateDecision() =>
        DecisionProposalFactory.FromCounterfactual(_org, _proposer,
            Evidence(1_000_000m, 250_000m, 40m), new ProposalJudgements(80m, 60m, 90, 50m), new DecisionScoringWeights());
}

using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;
using Xunit;

namespace Aurora.Tests.Knowledge;

public sealed class DecisionDebateEngineTests
{
    [Fact]
    public void DecisionThatMeetsEveryCriterionIsRecommendedWithoutObjection()
    {
        var outcome = DecisionDebateEngine.Run(new DebateCase(
            PredictedRevenueImpact: 100m, Confidence: 90m, DecisionScore: 80m, PredictionErrorPct: null, IsCounterfactualBacked: true));

        Assert.Equal(4, outcome.AdvocatePoints);
        Assert.Equal(0, outcome.CriticPoints);
        Assert.Equal(DecisionDebateEngine.ApprovalVerdict, outcome.Verdict);
    }

    [Fact]
    public void DecisionThatFailsEveryCriterionIsSentBackForRevision()
    {
        var outcome = DecisionDebateEngine.Run(new DebateCase(
            PredictedRevenueImpact: -5m, Confidence: 60m, DecisionScore: 50m, PredictionErrorPct: 30m, IsCounterfactualBacked: false));

        Assert.Equal(0, outcome.AdvocatePoints);
        Assert.Equal(5, outcome.CriticPoints);
        Assert.Equal(DecisionDebateEngine.RevisionVerdict, outcome.Verdict);
    }

    [Fact]
    public void TieGoesToRevision()
    {
        var outcome = DecisionDebateEngine.Run(new DebateCase(
            PredictedRevenueImpact: 100m, Confidence: 60m, DecisionScore: 80m, PredictionErrorPct: null, IsCounterfactualBacked: false));

        Assert.Equal(2, outcome.AdvocatePoints);
        Assert.Equal(2, outcome.CriticPoints);
        Assert.Equal(DecisionDebateEngine.RevisionVerdict, outcome.Verdict);
    }

    [Fact]
    public void ThresholdsAreInclusiveForTheAdvocateAndExclusiveForTheCritic()
    {
        var outcome = DecisionDebateEngine.Run(new DebateCase(
            PredictedRevenueImpact: 1m, Confidence: DecisionDebateEngine.MinimumConfidence,
            DecisionScore: DecisionDebateEngine.MinimumDecisionScore, PredictionErrorPct: DecisionDebateEngine.MaximumPredictionErrorPct,
            IsCounterfactualBacked: true));

        Assert.Equal(4, outcome.AdvocatePoints);
        Assert.Equal(0, outcome.CriticPoints);
    }

    [Fact]
    public void ArgumentsAreReturnedInRoleOrderWithTheJudgeLast()
    {
        var outcome = DecisionDebateEngine.Run(new DebateCase(10m, 75m, 65m, 5m, true));

        Assert.Equal(DebateRole.Advocate, outcome.Arguments[0].Role);
        Assert.Equal(DebateRole.Critic, outcome.Arguments[4].Role);
        Assert.Equal(DebateRole.Judge, outcome.Arguments[^1].Role);
        Assert.Equal(10, outcome.Arguments.Count);
    }
}

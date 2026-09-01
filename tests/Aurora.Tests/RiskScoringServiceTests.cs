using Aurora.Modules.Risk.Application;
using Aurora.Modules.Risk.Domain;
using Xunit;

namespace Aurora.Tests;

public class RiskScoringServiceTests
{
    [Fact]
    public void Score_IsZero_WhenProbabilityIsZero()
    {
        var weights = new RiskScoringWeights();
        var score = RiskScoringService.Score(probability: 0m, impact: 1m, exposure: 1m, criticality: 5, weights);
        Assert.Equal(0m, score);
    }

    [Fact]
    public void Score_IsMaximal_WhenAllFactorsAreMaximal()
    {
        var weights = new RiskScoringWeights();
        var score = RiskScoringService.Score(probability: 1m, impact: 1m, exposure: 1m, criticality: 5, weights);
        Assert.Equal(100m, score);
    }

    [Fact]
    public void Score_IncreasesMonotonically_WithCriticality()
    {
        var weights = new RiskScoringWeights();
        var lowCrit = RiskScoringService.Score(0.5m, 0.5m, 0.5m, 1, weights);
        var highCrit = RiskScoringService.Score(0.5m, 0.5m, 0.5m, 5, weights);
        Assert.True(highCrit > lowCrit);
    }
}

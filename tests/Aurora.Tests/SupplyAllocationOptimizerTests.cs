using Aurora.Modules.Optimization.Application;
using Aurora.Modules.Optimization.Domain;
using Xunit;

namespace Aurora.Tests;

public class SupplyAllocationOptimizerTests
{
    private static readonly IReadOnlyList<AllocationCandidate> Candidates = new[]
    {
        new AllocationCandidate("A", "Northbridge", Capacity: 800m, UnitCost: 12.50m, UnitRisk: 0.30m, MaxShare: 0.60m),
        new AllocationCandidate("B", "Vantage", Capacity: 600m, UnitCost: 14.00m, UnitRisk: 0.10m, MaxShare: 0.50m),
        new AllocationCandidate("C", "Halcyon", Capacity: 500m, UnitCost: 11.00m, UnitRisk: 0.45m, MaxShare: 0.40m),
    };

    [Fact]
    public void Recommendation_MeetsDemand_AndRespectsCapacityAndShareCaps()
    {
        var result = SupplyAllocationOptimizer.Optimize(1000m, Candidates, 0.5m);

        Assert.Equal(1000m, result.Recommended.TotalUnits);
        foreach (var line in result.Recommended.Lines)
        {
            var candidate = Candidates.Single(c => c.Id == line.CandidateId);
            Assert.True(line.Units <= candidate.Capacity);
            Assert.True(line.Units <= candidate.MaxShare * 1000m);
        }
    }

    [Fact]
    public void CostWeightZero_PrefersTheLowestRiskSourceFirst()
    {
        var result = SupplyAllocationOptimizer.Optimize(1000m, Candidates, 0m);

        // B has the lowest risk (0.10) and can supply 50% of demand, i.e. 500 units.
        Assert.Equal("B", result.Recommended.Lines[0].CandidateId);
        Assert.Equal(500m, result.Recommended.Lines[0].Units);
    }

    [Fact]
    public void CostWeightOne_PrefersTheCheapestSourceFirst()
    {
        var result = SupplyAllocationOptimizer.Optimize(1000m, Candidates, 1m);

        // C is the cheapest (11.00) and can supply 40% of demand, i.e. 400 units.
        Assert.Equal("C", result.Recommended.Lines[0].CandidateId);
        Assert.Equal(400m, result.Recommended.Lines[0].Units);
    }

    [Fact]
    public void Demand_BeyondDeliverableCapacity_IsInfeasible()
    {
        // Deliverable: min(800, 1200) + min(600, 1000) + min(500, 800) = 1900.
        Assert.Throws<InfeasibleAllocationException>(() => SupplyAllocationOptimizer.Optimize(2000m, Candidates, 0.5m));
    }

    [Fact]
    public void ParetoFrontier_ContainsNoDominatedPlan()
    {
        var frontier = SupplyAllocationOptimizer.Optimize(1000m, Candidates, 0.5m).ParetoFrontier;

        Assert.NotEmpty(frontier);
        foreach (var plan in frontier)
        {
            Assert.DoesNotContain(frontier, other =>
                other.TotalCost <= plan.TotalCost && other.TotalRiskExposure <= plan.TotalRiskExposure &&
                (other.TotalCost < plan.TotalCost || other.TotalRiskExposure < plan.TotalRiskExposure));
        }
    }

    [Fact]
    public void Optimize_RejectsDuplicateCandidateIdentifiers()
    {
        var duplicated = new[]
        {
            Candidates[0],
            Candidates[0] with { Name = "Copy" },
        };

        Assert.Throws<InvalidOptimizationRequestException>(() => SupplyAllocationOptimizer.Optimize(100m, duplicated, 0.5m));
    }

    [Fact]
    public void Optimize_RejectsCostWeightOutsideZeroToOne()
    {
        Assert.Throws<InvalidOptimizationRequestException>(() => SupplyAllocationOptimizer.Optimize(100m, Candidates, 1.5m));
    }
}

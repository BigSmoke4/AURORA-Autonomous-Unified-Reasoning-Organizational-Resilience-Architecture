using Aurora.Modules.Assets.Application;
using Aurora.Modules.Contracts.Application;
using Aurora.Modules.Customers.Application;
using Aurora.Modules.Markets.Application;
using Aurora.Modules.SupplyChain.Application;
using Xunit;

namespace Aurora.Tests.Modules;

public class CommercialCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Markets_Insufficient_WithOneObservation()
    {
        var result = MarketTrendCalculator.Evaluate(new[] { new MarketPoint(Start, 100m, 100m) });

        Assert.Equal(TrendDirection.Insufficient, result.DemandDirection);
        Assert.Null(result.DemandChangePct);
    }

    [Fact]
    public void Markets_Rising_WhenDemandGrowsBeyondTheStableBand()
    {
        var result = MarketTrendCalculator.Evaluate(new[]
        {
            new MarketPoint(Start, 100m, 100m),
            new MarketPoint(Start.AddDays(30), 105m, 95m),
        });

        Assert.Equal(TrendDirection.Rising, result.DemandDirection);
        Assert.Equal(5m, result.DemandChangePct);
        Assert.Equal(-5m, result.PriceChangePct);
    }

    [Fact]
    public void Markets_Stable_WithinTwoPercent()
    {
        var result = MarketTrendCalculator.Evaluate(new[]
        {
            new MarketPoint(Start, 100m, 100m),
            new MarketPoint(Start.AddDays(30), 101m, 100m),
        });

        Assert.Equal(TrendDirection.Stable, result.DemandDirection);
    }

    [Fact]
    public void Markets_Falling_WhenDemandDropsBeyondTheStableBand()
    {
        var result = MarketTrendCalculator.Evaluate(new[]
        {
            new MarketPoint(Start.AddDays(30), 90m, 100m),
            new MarketPoint(Start, 100m, 100m),
        });

        Assert.Equal(TrendDirection.Falling, result.DemandDirection);
        Assert.Equal(-10m, result.DemandChangePct);
    }

    [Fact]
    public void Markets_ComparesTheTwoMostRecentObservations_WhenGivenOutOfOrder()
    {
        var result = MarketTrendCalculator.Evaluate(new[]
        {
            new MarketPoint(Start.AddDays(60), 120m, 100m),
            new MarketPoint(Start, 50m, 100m),
            new MarketPoint(Start.AddDays(30), 100m, 100m),
        });

        Assert.Equal(3, result.ObservationCount);
        Assert.Equal(20m, result.DemandChangePct);
    }

    [Fact]
    public void Assets_FullyDepreciated_CapsAtLifeAndLeavesResidual()
    {
        var inService = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = StraightLineDepreciationCalculator.Evaluate(1000m, 100m, 9, inService, inService.AddDays(7305));

        Assert.Equal(900m, result.AccumulatedDepreciation);
        Assert.Equal(100m, result.NetBookValue);
        Assert.Equal(100m, result.AnnualCharge);
        Assert.True(result.FullyDepreciated);
        Assert.Equal(0m, result.RemainingLifeYears);
    }

    [Fact]
    public void Assets_PartiallyDepreciated_BeforeLifeEnds()
    {
        var inService = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = StraightLineDepreciationCalculator.Evaluate(1000m, 100m, 9, inService, inService.AddDays(1826));

        Assert.False(result.FullyDepreciated);
        Assert.True(result.NetBookValue > 100m && result.NetBookValue < 1000m);
    }

    [Fact]
    public void Assets_RejectsResidualAtOrAboveCost()
    {
        var inService = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StraightLineDepreciationCalculator.Evaluate(1000m, 1000m, 9, inService, inService));
    }

    [Fact]
    public void Contracts_NotStarted_BeforeTheStartDate()
    {
        var result = ContractStatusCalculator.Evaluate(Start, new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), 90,
            new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ContractState.NotStarted, result.State);
    }

    [Fact]
    public void Contracts_RenewalNoticeDue_WithinTheNoticePeriod()
    {
        var result = ContractStatusCalculator.Evaluate(Start, new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), 90,
            new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ContractState.RenewalNoticeDue, result.State);
        Assert.Equal(79, result.DaysRemaining);
    }

    [Fact]
    public void Contracts_Active_OutsideTheNoticePeriod()
    {
        var result = ContractStatusCalculator.Evaluate(Start, new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), 90,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ContractState.Active, result.State);
    }

    [Fact]
    public void Contracts_Expired_AfterTheEndDate()
    {
        var result = ContractStatusCalculator.Evaluate(Start, new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), 90,
            new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ContractState.Expired, result.State);
        Assert.Equal(-2, result.DaysRemaining);
    }

    [Fact]
    public void Contracts_PenaltyExposure_IsPenaltyPerDayTimesDelay()
    {
        Assert.Equal(6000m, ContractStatusCalculator.DelayPenaltyExposure(1500m, 4));
    }

    [Fact]
    public void Supply_ReorderPoint_IsDemandTimesLeadTimePlusSafetyStock()
    {
        var result = SupplyCoverageCalculator.Evaluate(100, 10m, 5, 20);

        Assert.Equal(70m, result.ReorderPointUnits);
        Assert.Equal(10m, result.DaysOfCover);
    }

    [Theory]
    [InlineData(0, InventoryState.Stockout)]
    [InlineData(15, InventoryState.BelowSafetyStock)]
    [InlineData(50, InventoryState.BelowReorderPoint)]
    [InlineData(100, InventoryState.Covered)]
    public void Supply_State_FollowsStockLevels(int onHand, InventoryState expected)
    {
        Assert.Equal(expected, SupplyCoverageCalculator.Evaluate(onHand, 10m, 5, 20).State);
    }

    [Fact]
    public void Supply_DaysOfCover_IsNullWithoutDemand()
    {
        var result = SupplyCoverageCalculator.Evaluate(100, 0m, 5, 20);

        Assert.Null(result.DaysOfCover);
        Assert.Equal(InventoryState.Covered, result.State);
    }

    [Fact]
    public void Supplier_NotStretched_JustBelowNinetyPercent()
    {
        Assert.False(SupplierCapacityRule.IsStretched(89.99m));
    }

    [Fact]
    public void Supplier_Stretched_AtNinetyPercent()
    {
        Assert.True(SupplierCapacityRule.IsStretched(90m));
    }

    [Fact]
    public void Concentration_HighlyConcentrated_ForASingleCustomer()
    {
        var result = CustomerConcentrationCalculator.Analyze(new[] { new CustomerExposure("Only", 500m) });

        Assert.Equal(10000m, result.Hhi);
        Assert.Equal(ConcentrationBand.HighlyConcentrated, result.Band);
        Assert.Equal(100m, result.TopCustomerSharePct);
    }

    [Fact]
    public void Concentration_Unconcentrated_ForTenEqualCustomers()
    {
        var customers = Enumerable.Range(1, 10).Select(i => new CustomerExposure($"C{i}", 100m)).ToList();

        var result = CustomerConcentrationCalculator.Analyze(customers);

        Assert.Equal(1000m, result.Hhi);
        Assert.Equal(ConcentrationBand.Unconcentrated, result.Band);
    }

    [Fact]
    public void Concentration_TopThreeShare_SumsTheThreeLargest()
    {
        var customers = new[]
        {
            new CustomerExposure("A", 50m),
            new CustomerExposure("B", 30m),
            new CustomerExposure("C", 10m),
            new CustomerExposure("D", 10m),
        };

        var result = CustomerConcentrationCalculator.Analyze(customers);

        Assert.Equal(90m, result.TopThreeSharePct);
        Assert.Equal(50m, result.TopCustomerSharePct);
    }

    [Fact]
    public void Concentration_EmptyPortfolio_IsUnconcentratedWithZeroHhi()
    {
        var result = CustomerConcentrationCalculator.Analyze(Array.Empty<CustomerExposure>());

        Assert.Equal(0m, result.Hhi);
        Assert.Equal(ConcentrationBand.Unconcentrated, result.Band);
    }

    [Fact]
    public void Concentration_RejectsNegativeExposure()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CustomerConcentrationCalculator.Analyze(new[] { new CustomerExposure("Bad", -1m) }));
    }
}

using Aurora.Shared.Kernel;

namespace Aurora.Modules.Assets.Application;

public sealed record DepreciationResult(
    decimal DepreciableAmount,
    decimal AnnualCharge,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    decimal ElapsedYears,
    decimal RemainingLifeYears,
    bool FullyDepreciated);

/// <summary>
/// Straight-line depreciation (spec section 24). The charge is (cost - residual) / life per year, and
/// elapsed time is whole days divided by the average year length. The accumulated figure is computed
/// from the exact elapsed time, so it is not distorted by rounding the annual charge.
/// </summary>
public static class StraightLineDepreciationCalculator
{
    public const decimal DaysPerYear = 365.25m;

    public static DepreciationResult Evaluate(decimal acquisitionCost, decimal residualValue, int usefulLifeYears, DateTime inServiceOnUtc, DateTime asOfUtc)
    {
        if (acquisitionCost <= 0m) throw new ArgumentOutOfRangeException(nameof(acquisitionCost), acquisitionCost, "The cost must be greater than zero.");
        if (residualValue < 0m || residualValue >= acquisitionCost)
            throw new ArgumentOutOfRangeException(nameof(residualValue), residualValue, "The residual value must be non-negative and below the cost.");
        if (usefulLifeYears < 1) throw new ArgumentOutOfRangeException(nameof(usefulLifeYears), usefulLifeYears, "The useful life must be at least one year.");

        var depreciable = acquisitionCost - residualValue;
        var elapsedDays = Math.Max(0, (asOfUtc.Date - inServiceOnUtc.Date).Days);
        var elapsedYears = Math.Min(usefulLifeYears, elapsedDays / DaysPerYear);

        var accumulated = Rounding.Round(depreciable * elapsedYears / usefulLifeYears);
        var netBookValue = acquisitionCost - accumulated;
        var remaining = Rounding.Round(Math.Max(0m, usefulLifeYears - elapsedYears));

        return new DepreciationResult(
            DepreciableAmount: depreciable,
            AnnualCharge: Rounding.Round(depreciable / usefulLifeYears),
            AccumulatedDepreciation: accumulated,
            NetBookValue: netBookValue,
            ElapsedYears: Rounding.Round(elapsedYears),
            RemainingLifeYears: remaining,
            FullyDepreciated: elapsedYears >= usefulLifeYears);
    }
}

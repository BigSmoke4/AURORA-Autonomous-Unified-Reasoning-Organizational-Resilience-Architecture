using Aurora.Shared.Kernel;

namespace Aurora.Modules.Finance.Application;

public enum BudgetStatus
{
    WithinBudget = 1,
    NearLimit = 2,
    Overspent = 3,
}

public sealed record BudgetPositionResult(decimal Variance, decimal? UtilizationPct, BudgetStatus Status);

/// <summary>
/// Deterministic budget position (spec section 20). Variance is budget minus spend, so a negative value
/// means overspend. Utilization is undefined for a zero budget.
/// </summary>
public static class BudgetPositionCalculator
{
    public const decimal NearLimitThresholdPct = 90m;

    public static BudgetPositionResult Evaluate(decimal annualBudget, decimal actualSpend)
    {
        if (annualBudget < 0m) throw new ArgumentOutOfRangeException(nameof(annualBudget), annualBudget, "The budget must not be negative.");
        if (actualSpend < 0m) throw new ArgumentOutOfRangeException(nameof(actualSpend), actualSpend, "The spend must not be negative.");

        var variance = Rounding.Round(annualBudget - actualSpend);
        decimal? utilization = annualBudget == 0m ? null : Rounding.Round(actualSpend * 100m / annualBudget);

        var status = actualSpend > annualBudget
            ? BudgetStatus.Overspent
            : utilization is { } pct && pct >= NearLimitThresholdPct
                ? BudgetStatus.NearLimit
                : BudgetStatus.WithinBudget;

        return new BudgetPositionResult(variance, utilization, status);
    }
}

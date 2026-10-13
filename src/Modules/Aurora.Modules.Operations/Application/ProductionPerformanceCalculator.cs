using Aurora.Shared.Kernel;

namespace Aurora.Modules.Operations.Application;

public enum AttainmentStatus
{
    Behind = 1,
    OnPlan = 2,
}

public enum YieldStatus
{
    NoOutput = 1,
    BelowTarget = 2,
    AtTarget = 3,
}

public sealed record ProductionPerformanceResult(
    decimal AttainmentPct,
    decimal? FirstPassYieldPct,
    AttainmentStatus AttainmentStatus,
    YieldStatus YieldStatus);

/// <summary>
/// Deterministic plan attainment and first-pass yield (spec section 21). Attainment is actual over planned.
/// First-pass yield is the share of produced units that were not defective. It is undefined with no output.
/// </summary>
public static class ProductionPerformanceCalculator
{
    public const decimal AttainmentTargetPct = 95m;
    public const decimal YieldTargetPct = 98m;

    public static ProductionPerformanceResult Evaluate(int plannedUnits, int actualUnits, int defectiveUnits)
    {
        if (plannedUnits < 1) throw new ArgumentOutOfRangeException(nameof(plannedUnits), plannedUnits, "Planned units must be at least one.");
        if (actualUnits < 0) throw new ArgumentOutOfRangeException(nameof(actualUnits), actualUnits, "Actual units must not be negative.");
        if (defectiveUnits < 0 || defectiveUnits > actualUnits)
            throw new ArgumentOutOfRangeException(nameof(defectiveUnits), defectiveUnits, "Defective units must be between zero and the actual units.");

        var attainment = Rounding.Round(actualUnits * 100m / plannedUnits);
        decimal? firstPassYield = actualUnits == 0 ? null : Rounding.Round((actualUnits - defectiveUnits) * 100m / actualUnits);

        var attainmentStatus = attainment < AttainmentTargetPct ? AttainmentStatus.Behind : AttainmentStatus.OnPlan;
        var yieldStatus = firstPassYield is not { } yieldPct
            ? YieldStatus.NoOutput
            : yieldPct < YieldTargetPct ? YieldStatus.BelowTarget : YieldStatus.AtTarget;

        return new ProductionPerformanceResult(attainment, firstPassYield, attainmentStatus, yieldStatus);
    }
}

using Aurora.Shared.Kernel;

namespace Aurora.Modules.SupplyChain.Application;

public enum InventoryState
{
    Stockout = 1,
    BelowSafetyStock = 2,
    BelowReorderPoint = 3,
    Covered = 4,
}

public sealed record SupplyCoverageResult(decimal ReorderPointUnits, decimal? DaysOfCover, InventoryState State);

/// <summary>
/// Deterministic inventory coverage (spec section 26). Reorder point is demand over the lead time plus safety
/// stock. Days of cover is on-hand divided by average daily demand, and is undefined when there is no demand.
/// </summary>
public static class SupplyCoverageCalculator
{
    public static SupplyCoverageResult Evaluate(int onHandUnits, decimal averageDailyDemand, int leadTimeDays, int safetyStockUnits)
    {
        if (onHandUnits < 0) throw new ArgumentOutOfRangeException(nameof(onHandUnits), onHandUnits, "On-hand units must not be negative.");
        if (averageDailyDemand < 0m) throw new ArgumentOutOfRangeException(nameof(averageDailyDemand), averageDailyDemand, "Demand must not be negative.");
        if (leadTimeDays < 0) throw new ArgumentOutOfRangeException(nameof(leadTimeDays), leadTimeDays, "The lead time must not be negative.");
        if (safetyStockUnits < 0) throw new ArgumentOutOfRangeException(nameof(safetyStockUnits), safetyStockUnits, "Safety stock must not be negative.");

        var reorderPoint = Rounding.Round(averageDailyDemand * leadTimeDays + safetyStockUnits);
        decimal? daysOfCover = averageDailyDemand == 0m ? null : Rounding.Round(onHandUnits / averageDailyDemand);

        var state = onHandUnits == 0 && averageDailyDemand > 0m ? InventoryState.Stockout
            : onHandUnits < safetyStockUnits ? InventoryState.BelowSafetyStock
            : onHandUnits < reorderPoint ? InventoryState.BelowReorderPoint
            : InventoryState.Covered;

        return new SupplyCoverageResult(reorderPoint, daysOfCover, state);
    }
}

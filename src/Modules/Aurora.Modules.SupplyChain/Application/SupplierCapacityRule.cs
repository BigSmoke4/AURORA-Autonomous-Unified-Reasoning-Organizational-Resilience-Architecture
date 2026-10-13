namespace Aurora.Modules.SupplyChain.Application;

/// <summary>Flags suppliers running close to full capacity, where a demand spike would cause shortages (spec section 26).</summary>
public static class SupplierCapacityRule
{
    public const decimal StretchedThresholdPct = 90m;

    public static bool IsStretched(decimal capacityUtilizationPct) => capacityUtilizationPct >= StretchedThresholdPct;
}

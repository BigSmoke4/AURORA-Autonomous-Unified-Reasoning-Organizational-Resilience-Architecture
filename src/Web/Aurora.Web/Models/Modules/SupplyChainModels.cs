using Aurora.Modules.SupplyChain.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class InventoryPositionForm
{
    public string Sku { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int OnHandUnits { get; set; }
    public decimal AverageDailyDemand { get; set; }
    public int ReplenishmentLeadTimeDays { get; set; }
    public int SafetyStockUnits { get; set; }
}

public sealed record InventoryRow(
    string Sku,
    string Location,
    int OnHandUnits,
    decimal AverageDailyDemand,
    int LeadTimeDays,
    int SafetyStockUnits,
    decimal ReorderPointUnits,
    decimal? DaysOfCover,
    string State,
    string Tone);

public sealed record StretchedSupplierRow(string Name, string? Location, decimal UtilizationPct);

public sealed record SupplyChainIndexViewModel(
    IReadOnlyList<InventoryRow> Rows,
    IReadOnlyList<StretchedSupplierRow> StretchedSuppliers,
    InventoryPositionForm Form);

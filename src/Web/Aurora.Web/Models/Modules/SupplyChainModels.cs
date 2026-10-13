using System.ComponentModel.DataAnnotations;
using Aurora.Modules.SupplyChain.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class InventoryPositionForm
{
    [Required]
    [StringLength(InventoryPosition.SkuMaxLength)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(InventoryPosition.LocationMaxLength)]
    public string Location { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int OnHandUnits { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumDemand)]
    public decimal AverageDailyDemand { get; set; }

    [Range(0, InventoryPosition.MaxLeadTimeDays)]
    public int ReplenishmentLeadTimeDays { get; set; }

    [Range(0, int.MaxValue)]
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

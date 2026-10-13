using Aurora.Modules.Assets.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class PhysicalAssetForm
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal AcquisitionCost { get; set; }
    public decimal ResidualValue { get; set; }
    public DateTime InServiceOn { get; set; }
    public int UsefulLifeYears { get; set; } = PhysicalAsset.MinUsefulLifeYears;
}

public sealed record AssetRow(
    string Name,
    string Category,
    decimal Cost,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    decimal AnnualCharge,
    decimal RemainingLifeYears,
    bool FullyDepreciated);

public sealed record AssetsIndexViewModel(
    IReadOnlyList<AssetRow> Rows,
    decimal TotalCost,
    decimal TotalNetBookValue,
    PhysicalAssetForm Form);

using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Assets.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class PhysicalAssetForm : IValidatableObject
{
    [Required]
    [StringLength(PhysicalAsset.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(PhysicalAsset.CategoryMaxLength)]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), ModuleFormatting.MinimumPositiveMoney, ModuleFormatting.MaximumMoney)]
    public decimal AcquisitionCost { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney)]
    public decimal ResidualValue { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime InServiceOn { get; set; }

    [Range(PhysicalAsset.MinUsefulLifeYears, PhysicalAsset.MaxUsefulLifeYears)]
    public int UsefulLifeYears { get; set; } = PhysicalAsset.MinUsefulLifeYears;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ResidualValue >= AcquisitionCost)
            yield return new ValidationResult("The residual value must be below the acquisition cost.", [nameof(ResidualValue)]);
    }
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

using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Finance.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class CostCenterForm
{
    [Required]
    [StringLength(CostCenter.CodeMaxLength)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(CostCenter.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney)]
    public decimal AnnualBudget { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney)]
    public decimal ActualSpend { get; set; }
}

public sealed record CostCenterRow(
    string Code,
    string Name,
    decimal Budget,
    decimal Spend,
    decimal Variance,
    decimal? UtilizationPct,
    string Status,
    string Tone);

public sealed record FinanceIndexViewModel(
    IReadOnlyList<CostCenterRow> Rows,
    decimal TotalBudget,
    decimal TotalSpend,
    CostCenterForm Form);

using Aurora.Modules.Finance.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class CostCenterForm
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AnnualBudget { get; set; }
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

using Aurora.Modules.Contracts.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class SupplierContractForm
{
    public string Reference { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public DateTime StartsOn { get; set; }
    public DateTime EndsOn { get; set; }
    public decimal AnnualValue { get; set; }
    public decimal LatePenaltyPerDay { get; set; }
    public int NoticePeriodDays { get; set; }
}

public sealed record ContractRow(
    string Reference,
    string Counterparty,
    DateTime StartsOnUtc,
    DateTime EndsOnUtc,
    int DaysRemaining,
    string State,
    string Tone,
    decimal AnnualValue,
    decimal PenaltyExposure);

public sealed record ContractsIndexViewModel(IReadOnlyList<ContractRow> Rows, int DelayDays, SupplierContractForm Form);

using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Contracts.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class SupplierContractForm : IValidatableObject
{
    [Required]
    [StringLength(SupplierContract.ReferenceMaxLength)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [StringLength(SupplierContract.CounterpartyMaxLength)]
    public string Counterparty { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime StartsOn { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime EndsOn { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney)]
    public decimal AnnualValue { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney)]
    public decimal LatePenaltyPerDay { get; set; }

    [Range(0, SupplierContract.MaxNoticePeriodDays)]
    public int NoticePeriodDays { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsOn < StartsOn)
            yield return new ValidationResult("The end date cannot precede the start date.", [nameof(EndsOn)]);
    }
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

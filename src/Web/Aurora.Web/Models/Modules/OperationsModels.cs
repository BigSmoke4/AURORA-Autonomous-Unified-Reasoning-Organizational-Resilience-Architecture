using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Modules;

public sealed class ProductionRecordForm : IValidatableObject
{
    [Required]
    public Guid? FactoryId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime PeriodStart { get; set; }

    [Range(1, int.MaxValue)]
    public int PlannedUnits { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int ActualUnits { get; set; }

    [Range(0, int.MaxValue)]
    public int DefectiveUnits { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DefectiveUnits > ActualUnits)
            yield return new ValidationResult("Defective units cannot exceed actual units.", [nameof(DefectiveUnits)]);
    }
}

public sealed record ProductionRow(
    string Factory,
    DateTime PeriodStartUtc,
    int PlannedUnits,
    int ActualUnits,
    int DefectiveUnits,
    decimal AttainmentPct,
    decimal? FirstPassYieldPct,
    string AttainmentStatus,
    string YieldStatus,
    string Tone);

public sealed record OperationsIndexViewModel(
    IReadOnlyList<ProductionRow> Rows,
    IReadOnlyList<SelectListItem> Factories,
    ProductionRecordForm Form);

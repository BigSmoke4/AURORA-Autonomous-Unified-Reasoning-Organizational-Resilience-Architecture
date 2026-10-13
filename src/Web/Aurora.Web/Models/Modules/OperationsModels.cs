using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Modules;

public sealed class ProductionRecordForm
{
    public Guid? FactoryId { get; set; }
    public DateTime PeriodStart { get; set; }
    public int PlannedUnits { get; set; } = 1;
    public int ActualUnits { get; set; }
    public int DefectiveUnits { get; set; }
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

using Aurora.Modules.Monitoring.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Modules;

public sealed class MetricDefinitionForm
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? MinimumThreshold { get; set; }
    public decimal? MaximumThreshold { get; set; }
}

public sealed class MetricReadingForm
{
    public Guid? MetricDefinitionId { get; set; }
    public decimal Value { get; set; }
    public DateTime RecordedOn { get; set; }
}

public sealed record MetricRow(
    Guid Id,
    string Name,
    string Unit,
    decimal? Minimum,
    decimal? Maximum,
    decimal? LatestValue,
    DateTime? LatestRecordedAtUtc,
    string State,
    string Tone);

public sealed record MonitoringIndexViewModel(
    IReadOnlyList<MetricRow> Rows,
    IReadOnlyList<SelectListItem> Metrics,
    MetricDefinitionForm DefinitionForm,
    MetricReadingForm ReadingForm);

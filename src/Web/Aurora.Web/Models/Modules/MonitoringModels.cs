using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Monitoring.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Modules;

public sealed class MetricDefinitionForm : IValidatableObject
{
    [Required]
    [StringLength(MetricDefinition.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(MetricDefinition.UnitMaxLength)]
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), ModuleFormatting.MinimumReadingValue, ModuleFormatting.MaximumReadingValue)]
    public decimal? MinimumThreshold { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumReadingValue, ModuleFormatting.MaximumReadingValue)]
    public decimal? MaximumThreshold { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinimumThreshold is null && MaximumThreshold is null)
            yield return new ValidationResult("Set at least one threshold.", [nameof(MinimumThreshold), nameof(MaximumThreshold)]);
        else if (MinimumThreshold is { } low && MaximumThreshold is { } high && low >= high)
            yield return new ValidationResult("The minimum threshold must be below the maximum threshold.", [nameof(MinimumThreshold)]);
    }
}

public sealed class MetricReadingForm
{
    [Required]
    public Guid? MetricDefinitionId { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumReadingValue, ModuleFormatting.MaximumReadingValue)]
    public decimal Value { get; set; }

    [Required]
    [DataType(DataType.Date)]
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

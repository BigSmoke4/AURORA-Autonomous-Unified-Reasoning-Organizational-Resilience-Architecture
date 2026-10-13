using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Markets.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class MarketObservationForm
{
    [Required]
    [StringLength(MarketObservation.MarketMaxLength)]
    public string Market { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime ObservedOn { get; set; }

    [Range(typeof(decimal), ModuleFormatting.MinimumIndex, ModuleFormatting.MaximumIndex)]
    public decimal DemandIndex { get; set; } = 100m;

    [Range(typeof(decimal), ModuleFormatting.MinimumIndex, ModuleFormatting.MaximumIndex)]
    public decimal PriceIndex { get; set; } = 100m;
}

public sealed record MarketRow(
    string Market,
    int Observations,
    DateTime LatestOnUtc,
    decimal? DemandChangePct,
    decimal? PriceChangePct,
    string Direction);

public sealed record MarketsIndexViewModel(IReadOnlyList<MarketRow> Rows, MarketObservationForm Form);

using Aurora.Modules.Markets.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class MarketObservationForm
{
    public string Market { get; set; } = string.Empty;
    public DateTime ObservedOn { get; set; }
    public decimal DemandIndex { get; set; } = 100m;
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

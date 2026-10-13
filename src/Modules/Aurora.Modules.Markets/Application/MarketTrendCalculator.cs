using Aurora.Shared.Kernel;

namespace Aurora.Modules.Markets.Application;

public enum TrendDirection
{
    Insufficient = 1,
    Rising = 2,
    Falling = 3,
    Stable = 4,
}

public sealed record MarketPoint(DateTime ObservedOnUtc, decimal DemandIndex, decimal PriceIndex);

public sealed record MarketTrendResult(
    int ObservationCount,
    decimal? DemandChangePct,
    decimal? PriceChangePct,
    TrendDirection DemandDirection);

/// <summary>
/// Deterministic trend over the two most recent observations (spec section 23). Change is the latest value
/// relative to the one before it. A move within the stable band is reported as stable, not as a trend.
/// </summary>
public static class MarketTrendCalculator
{
    public const decimal StableBandPct = 2m;

    public static MarketTrendResult Evaluate(IReadOnlyList<MarketPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2) return new MarketTrendResult(points.Count, null, null, TrendDirection.Insufficient);

        var ordered = points.OrderBy(p => p.ObservedOnUtc).ToList();
        var previous = ordered[^2];
        var latest = ordered[^1];

        var demandChange = ChangePct(previous.DemandIndex, latest.DemandIndex);
        var priceChange = ChangePct(previous.PriceIndex, latest.PriceIndex);

        var direction = demandChange is not { } change
            ? TrendDirection.Insufficient
            : change >= StableBandPct ? TrendDirection.Rising
            : change <= -StableBandPct ? TrendDirection.Falling
            : TrendDirection.Stable;

        return new MarketTrendResult(points.Count, demandChange, priceChange, direction);
    }

    private static decimal? ChangePct(decimal previous, decimal latest) =>
        previous == 0m ? null : Rounding.Round((latest - previous) * 100m / previous);
}

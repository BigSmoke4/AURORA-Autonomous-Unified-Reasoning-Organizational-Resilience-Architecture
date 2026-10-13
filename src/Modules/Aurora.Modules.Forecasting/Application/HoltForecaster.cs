using Aurora.Modules.Forecasting.Domain;

namespace Aurora.Modules.Forecasting.Application;

public sealed class InvalidForecastRequestException(string message) : Exception(message);

/// <summary>
/// Deterministic Holt linear-trend forecaster (spec section 15). The model:
///   level_t = a*y_t + (1-a)*(level_t-1 + trend_t-1)
///   trend_t = b*(level_t - level_t-1) + (1-b)*trend_t-1
///   forecast_(t+h) = level_t + h*trend_t
/// Smoothing constants a and b are chosen by an exhaustive search over a fixed 0.1-step grid,
/// minimizing one-step-ahead squared error. The search order is fixed, so equal inputs always
/// give equal outputs.
///
/// Prediction intervals are approximate: they assume roughly normal one-step residuals and widen
/// with the square root of the horizon. They do not model structural breaks, so treat them as a
/// guide to uncertainty, not a guarantee.
/// </summary>
public static class HoltForecaster
{
    public const string ModelVersion = "holt-linear-v1";
    public const int MinimumObservations = 6;
    public const int MaximumHorizon = 24;
    public const int MaximumObservations = 520;

    private const int ResultDecimals = 4;
    private const int HoldoutDivisor = 4;
    private const int SmoothingGridSteps = 9;
    private const decimal SmoothingGridStep = 0.1m;

    // Two-sided normal quantiles for the supported confidence levels.
    private static readonly IReadOnlyDictionary<decimal, double> NormalQuantiles = new Dictionary<decimal, double>
    {
        [0.80m] = 1.2815515655446004,
        [0.90m] = 1.6448536269514722,
        [0.95m] = 1.9599639845400543,
        [0.99m] = 2.5758293035489004,
    };

    private static readonly decimal[] SmoothingGrid = Enumerable.Range(1, SmoothingGridSteps)
        .Select(i => i * SmoothingGridStep).ToArray();

    public static IReadOnlyList<decimal> SupportedConfidenceLevels => NormalQuantiles.Keys.OrderBy(k => k).ToArray();

    public static ForecastResult Forecast(IReadOnlyList<decimal> series, int horizon, decimal confidenceLevel)
    {
        if (series.Count < MinimumObservations)
            throw new InvalidForecastRequestException($"At least {MinimumObservations} observations are required.");
        if (series.Count > MaximumObservations)
            throw new InvalidForecastRequestException($"At most {MaximumObservations} observations are accepted.");
        if (horizon is < 1 or > MaximumHorizon)
            throw new InvalidForecastRequestException($"Horizon must be between 1 and {MaximumHorizon} periods.");
        if (!NormalQuantiles.TryGetValue(confidenceLevel, out var z))
            throw new InvalidForecastRequestException("Confidence level must be one of 0.80, 0.90, 0.95 or 0.99.");

        var fit = Fit(series);
        var residualCount = series.Count - 1;
        var residualStdDev = Math.Sqrt((double)fit.Sse / Math.Max(1, residualCount - 2));

        var points = new List<ForecastPoint>(horizon);
        for (var h = 1; h <= horizon; h++)
        {
            var forecast = fit.Level + h * fit.Trend;
            var halfWidth = (decimal)(z * residualStdDev * Math.Sqrt(h));
            points.Add(new ForecastPoint(h,
                Math.Round(forecast, ResultDecimals),
                Math.Round(forecast - halfWidth, ResultDecimals),
                Math.Round(forecast + halfWidth, ResultDecimals)));
        }

        return new ForecastResult(
            ModelVersion,
            series.Count,
            fit.Alpha,
            fit.Beta,
            Math.Round(fit.Level, ResultDecimals),
            Math.Round(fit.Trend, ResultDecimals),
            Math.Round((decimal)residualStdDev, ResultDecimals),
            confidenceLevel,
            Backtest(series),
            points);
    }

    private sealed record FitResult(decimal Alpha, decimal Beta, decimal Level, decimal Trend, decimal Sse);

    private static FitResult Fit(IReadOnlyList<decimal> series)
    {
        FitResult? best = null;
        foreach (var alpha in SmoothingGrid)
        {
            foreach (var beta in SmoothingGrid)
            {
                var candidate = Run(series, alpha, beta);
                if (best is null || candidate.Sse < best.Sse) best = candidate;
            }
        }
        return best!;
    }

    private static FitResult Run(IReadOnlyList<decimal> series, decimal alpha, decimal beta)
    {
        var level = series[0];
        var trend = series[1] - series[0];
        decimal sse = 0m;

        for (var t = 1; t < series.Count; t++)
        {
            var predicted = level + trend;
            var error = series[t] - predicted;
            sse += error * error;

            var newLevel = alpha * series[t] + (1m - alpha) * predicted;
            trend = beta * (newLevel - level) + (1m - beta) * trend;
            level = newLevel;
        }
        return new FitResult(alpha, beta, level, trend, sse);
    }

    /// <summary>Fits on all but the last holdout points, forecasts the holdout, and reports MAPE.</summary>
    private static decimal? Backtest(IReadOnlyList<decimal> series)
    {
        var holdout = Math.Max(1, series.Count / HoldoutDivisor);
        var training = series.Take(series.Count - holdout).ToList();
        if (training.Count < MinimumObservations) return null;

        var fit = Fit(training);
        decimal absolutePercentageSum = 0m;
        var counted = 0;
        for (var h = 1; h <= holdout; h++)
        {
            var actual = series[training.Count + h - 1];
            if (actual == 0m) continue;
            var forecast = fit.Level + h * fit.Trend;
            absolutePercentageSum += Math.Abs((actual - forecast) / actual);
            counted++;
        }
        if (counted == 0) return null;
        return Math.Round(absolutePercentageSum / counted * 100m, 2);
    }
}

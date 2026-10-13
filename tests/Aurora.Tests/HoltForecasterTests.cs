using Aurora.Modules.Forecasting.Application;
using Xunit;

namespace Aurora.Tests;

public class HoltForecasterTests
{
    private static readonly decimal[] LinearSeries = Enumerable.Range(1, 12).Select(i => (decimal)i).ToArray();

    private static readonly decimal[] NoisySeries =
    {
        120m, 124m, 131m, 129m, 138m, 142m, 147m, 151m, 149m, 158m, 163m, 170m,
    };

    [Fact]
    public void ConstantSeries_ForecastsTheConstant_WithZeroWidthIntervals()
    {
        var series = Enumerable.Repeat(100m, 10).ToArray();

        var result = HoltForecaster.Forecast(series, horizon: 3, confidenceLevel: 0.95m);

        Assert.All(result.Points, p =>
        {
            Assert.Equal(100m, p.Forecast);
            Assert.Equal(100m, p.Lower);
            Assert.Equal(100m, p.Upper);
        });
        Assert.Equal(0m, result.BacktestMapePct);
    }

    [Fact]
    public void LinearSeries_IsExtrapolatedExactly()
    {
        var result = HoltForecaster.Forecast(LinearSeries, horizon: 2, confidenceLevel: 0.95m);

        Assert.Equal(13m, result.Points[0].Forecast);
        Assert.Equal(14m, result.Points[1].Forecast);
        Assert.Equal(0m, result.ResidualStdDev);
    }

    [Fact]
    public void Forecast_IsDeterministic_ForEqualInputs()
    {
        var first = HoltForecaster.Forecast(NoisySeries, horizon: 6, confidenceLevel: 0.9m);
        var second = HoltForecaster.Forecast(NoisySeries, horizon: 6, confidenceLevel: 0.9m);

        Assert.Equal(first.Alpha, second.Alpha);
        Assert.Equal(first.Beta, second.Beta);
        Assert.Equal(first.Points.Select(p => (p.Forecast, p.Lower, p.Upper)),
                     second.Points.Select(p => (p.Forecast, p.Lower, p.Upper)));
    }

    [Fact]
    public void IntervalWidth_GrowsWithConfidenceLevel()
    {
        var narrow = HoltForecaster.Forecast(NoisySeries, horizon: 1, confidenceLevel: 0.80m).Points[0];
        var wide = HoltForecaster.Forecast(NoisySeries, horizon: 1, confidenceLevel: 0.99m).Points[0];

        Assert.True(wide.Upper - wide.Lower > narrow.Upper - narrow.Lower);
    }

    [Fact]
    public void IntervalWidth_GrowsWithSquareRootOfHorizon()
    {
        var points = HoltForecaster.Forecast(NoisySeries, horizon: 4, confidenceLevel: 0.95m).Points;
        var widthAtOne = points[0].Upper - points[0].Lower;
        var widthAtFour = points[3].Upper - points[3].Lower;

        // sqrt(4) = 2, so the width at step 4 is twice the width at step 1, up to rounding.
        Assert.InRange(widthAtFour, widthAtOne * 2m - 0.001m, widthAtOne * 2m + 0.001m);
    }

    [Fact]
    public void Forecast_RejectsShortSeries()
    {
        var series = new[] { 1m, 2m, 3m, 4m, 5m };

        Assert.Throws<InvalidForecastRequestException>(() => HoltForecaster.Forecast(series, 1, 0.95m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Forecast_RejectsHorizonOutsideSupportedRange(int horizon)
    {
        Assert.Throws<InvalidForecastRequestException>(() => HoltForecaster.Forecast(NoisySeries, horizon, 0.95m));
    }

    [Fact]
    public void Forecast_RejectsUnsupportedConfidenceLevel()
    {
        Assert.Throws<InvalidForecastRequestException>(() => HoltForecaster.Forecast(NoisySeries, 3, 0.5m));
    }

    [Fact]
    public void Backtest_IsNull_WhenHoldoutWouldLeaveTooFewTrainingPoints()
    {
        var result = HoltForecaster.Forecast(new[] { 10m, 11m, 12m, 13m, 14m, 15m }, 1, 0.95m);

        Assert.Null(result.BacktestMapePct);
    }
}

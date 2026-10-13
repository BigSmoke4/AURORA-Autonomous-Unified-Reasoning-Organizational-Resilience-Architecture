using Aurora.Modules.Forecasting.Application;

namespace Aurora.Web.Models.Forecasting;

public sealed class ForecastForm
{
    public const int MaxSeriesTextLength = 4000;
    public string SeriesText { get; set; } = "120, 124, 131, 129, 138, 142, 147, 151, 149, 158, 163, 170";
    public int Horizon { get; set; } = 6;

    public decimal ConfidenceLevel { get; set; } = 0.95m;

    public IReadOnlyList<decimal> ConfidenceOptions { get; set; } = HoltForecaster.SupportedConfidenceLevels;
}

namespace Aurora.Modules.Forecasting.Domain;

public sealed record ForecastPoint(int StepAhead, decimal Forecast, decimal Lower, decimal Upper);

/// <summary>
/// Output of one forecast. BacktestMapePct is measured on a holdout of the most recent observations
/// that the model was not fitted on; it is null when the holdout has no non-zero actuals.
/// </summary>
public sealed record ForecastResult(
    string ModelVersion,
    int Observations,
    decimal Alpha,
    decimal Beta,
    decimal Level,
    decimal Trend,
    decimal ResidualStdDev,
    decimal ConfidenceLevel,
    decimal? BacktestMapePct,
    IReadOnlyList<ForecastPoint> Points);

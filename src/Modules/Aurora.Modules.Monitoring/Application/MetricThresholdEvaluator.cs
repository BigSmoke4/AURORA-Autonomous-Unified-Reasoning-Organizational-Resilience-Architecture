namespace Aurora.Modules.Monitoring.Application;

public enum ThresholdState
{
    Normal = 1,
    BelowMinimum = 2,
    AboveMaximum = 3,
}

/// <summary>Deterministic threshold check for one reading (spec section 27). Bounds are inclusive.</summary>
public static class MetricThresholdEvaluator
{
    public static ThresholdState Evaluate(decimal value, decimal? minimum, decimal? maximum)
    {
        if (minimum is { } low && value < low) return ThresholdState.BelowMinimum;
        if (maximum is { } high && value > high) return ThresholdState.AboveMaximum;
        return ThresholdState.Normal;
    }
}

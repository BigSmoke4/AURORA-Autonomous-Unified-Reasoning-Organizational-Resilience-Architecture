namespace Aurora.Modules.Alerts.Application;

/// <summary>
/// Stable keys that tie an alert to the object that raised it. An open alert with the same key is not raised
/// again, so a metric that stays out of range produces one alert rather than one per reading.
/// </summary>
public static class AlertSourceKeys
{
    public static string ForMetric(Guid metricDefinitionId) => $"monitoring:metric:{metricDefinitionId:D}";
}

using System.Globalization;
using System.Text.Json;

namespace Aurora.Web.Integration.Feeds;

public sealed record MetricFeedRow(int Index, string Metric, DateTime ObservedOnUtc, decimal Value);

public sealed record MetricFeedParseResult(IReadOnlyList<MetricFeedRow> Accepted, IReadOnlyList<string> Rejected);

/// <summary>
/// Parses a metric feed payload with a fixed schema. A malformed document fails the whole payload. An invalid row is
/// rejected alone, with its index and the reason, so one bad row cannot stop the rest of the feed.
/// </summary>
public static class MetricFeedParser
{
    public const int SupportedSchemaVersion = 1;
    public const int MaxRows = 1000;
    public const int MaxMetricNameLength = 200;
    private const string DateFormat = "yyyy-MM-dd";

    public static MetricFeedParseResult Parse(string json, DateOnly asOf)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("The feed payload is not valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new FormatException("The feed payload must be a JSON object.");
            if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schema) || schema != SupportedSchemaVersion)
            {
                throw new FormatException($"The feed payload must declare schemaVersion {SupportedSchemaVersion}.");
            }

            if (!root.TryGetProperty("readings", out var readings) || readings.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("The feed payload must contain a readings array.");
            }

            if (readings.GetArrayLength() > MaxRows)
            {
                throw new FormatException($"The feed payload has more than {MaxRows} rows.");
            }

            var accepted = new List<MetricFeedRow>();
            var rejected = new List<string>();
            var index = 0;
            foreach (var row in readings.EnumerateArray())
            {
                index++;
                var outcome = ParseRow(index, row, asOf);
                if (outcome.Row is { } parsed) accepted.Add(parsed);
                else rejected.Add(outcome.Reason!);
            }

            return new MetricFeedParseResult(accepted, rejected);
        }
    }

    private static (MetricFeedRow? Row, string? Reason) ParseRow(int index, JsonElement row, DateOnly asOf)
    {
        if (row.ValueKind != JsonValueKind.Object) return (null, $"Row {index}: must be an object.");

        if (!row.TryGetProperty("metric", out var metricElement) || metricElement.ValueKind != JsonValueKind.String)
            return (null, $"Row {index}: metric must be text.");
        var metric = metricElement.GetString()?.Trim() ?? string.Empty;
        if (metric.Length == 0 || metric.Length > MaxMetricNameLength)
            return (null, $"Row {index}: metric must be 1 to {MaxMetricNameLength} characters.");

        if (!row.TryGetProperty("observedOn", out var dateElement) || dateElement.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(dateElement.GetString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var observedOn))
            return (null, $"Row {index}: observedOn must be a date in {DateFormat} format.");
        if (observedOn > asOf) return (null, $"Row {index}: observedOn is after the ingestion date.");

        if (!row.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Number
            || !valueElement.TryGetDecimal(out var value))
            return (null, $"Row {index}: value must be a decimal number.");

        var observedOnUtc = DateTime.SpecifyKind(observedOn.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        return (new MetricFeedRow(index, metric, observedOnUtc, value), null);
    }
}

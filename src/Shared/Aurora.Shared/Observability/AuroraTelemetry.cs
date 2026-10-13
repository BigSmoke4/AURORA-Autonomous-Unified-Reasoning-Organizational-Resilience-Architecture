using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aurora.Shared.Observability;

/// <summary>
/// AURORA's custom telemetry vocabulary (spec section 68). Modules record spans and metrics
/// through these instruments so the OpenTelemetry pipeline in Aurora.Web can export them
/// without any module depending on an OpenTelemetry SDK package.
/// </summary>
public static class AuroraTelemetry
{
    public const string SourceName = "Aurora";
    public const string MeterName = "Aurora";

    public static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> SimulationDurationMs =
        Meter.CreateHistogram<double>("aurora.simulation.duration", "ms", "Wall-clock duration of a deterministic simulation run.");

    public static readonly Histogram<double> OptimizationDurationMs =
        Meter.CreateHistogram<double>("aurora.optimization.duration", "ms", "Wall-clock duration of an allocation optimization run.");

    public static readonly Histogram<double> InvestigationDurationMs =
        Meter.CreateHistogram<double>("aurora.investigation.duration", "ms", "Wall-clock duration of an AI investigation, including tool calls.");

    public static readonly Histogram<double> AiProviderLatencyMs =
        Meter.CreateHistogram<double>("aurora.ai.provider.latency", "ms", "Latency of calls to the configured LLM provider.");

    public static readonly Counter<long> AiProviderFailures =
        Meter.CreateCounter<long>("aurora.ai.provider.failures", "{failure}", "Failed LLM provider calls that degraded to deterministic output.");
}

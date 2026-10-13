using System.Text.Json;
using Aurora.Web.Infrastructure.Jobs;

namespace Aurora.Web.Integration.Feeds;

/// <summary>Runs a metric feed ingestion from the job queue. The payload names the adapter to fetch from.</summary>
public sealed class MetricFeedJobHandler(IEnumerable<IExternalFeedAdapter> adapters, MetricFeedIngestor ingestor) : IBackgroundJobHandler
{
    public const string JobKind = "feeds.metrics.ingest";

    public string Kind => JobKind;

    public static string BuildPayload(string sourceKey) => JsonSerializer.Serialize(new FeedJobPayload(sourceKey));

    public async Task<string> HandleAsync(Guid organizationId, string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<FeedJobPayload>(payloadJson)
            ?? throw new InvalidOperationException("The feed job payload is empty.");
        var adapter = adapters.FirstOrDefault(a => a.SourceKey == payload.SourceKey)
            ?? throw new InvalidOperationException($"No feed adapter is registered for '{payload.SourceKey}'.");

        var body = await adapter.FetchAsync(ct);
        var summary = await ingestor.IngestAsync(organizationId, adapter.SourceKey, body, ct);
        return summary.Describe();
    }

    public sealed record FeedJobPayload(string SourceKey);
}

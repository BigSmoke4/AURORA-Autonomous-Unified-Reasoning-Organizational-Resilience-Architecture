using Microsoft.Extensions.Options;

namespace Aurora.Web.Integration.Feeds;

/// <summary>Fetches a metric feed payload from the configured live endpoint.</summary>
public sealed class HttpMetricFeedAdapter(HttpClient client, IOptions<ExternalFeedOptions> options) : IExternalFeedAdapter
{
    public const string Key = "http-metrics";

    public string SourceKey => Key;

    public FeedKind Kind => FeedKind.Metrics;

    public Task<string> FetchAsync(CancellationToken ct) =>
        HttpFeedClient.GetPayloadAsync(client, options.Value.Metrics, Key, ct);
}

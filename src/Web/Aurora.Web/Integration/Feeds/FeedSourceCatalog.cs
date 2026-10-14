using Microsoft.Extensions.Options;

namespace Aurora.Web.Integration.Feeds;

/// <summary>
/// Decides which source a queued ingestion uses. The live adapter is used when its endpoint is configured, and the
/// fixture adapter otherwise. The choice is made when the job is queued, and it is recorded in the job payload.
/// </summary>
public sealed class FeedSourceCatalog(IOptions<ExternalFeedOptions> options)
{
    public string MetricSourceKey => options.Value.Metrics.IsConfigured ? HttpMetricFeedAdapter.Key : FixtureMetricFeedAdapter.Key;

    public string VulnerabilitySourceKey =>
        options.Value.Vulnerabilities.IsConfigured ? HttpVulnerabilityFeedAdapter.Key : FixtureVulnerabilityFeedAdapter.Key;
}

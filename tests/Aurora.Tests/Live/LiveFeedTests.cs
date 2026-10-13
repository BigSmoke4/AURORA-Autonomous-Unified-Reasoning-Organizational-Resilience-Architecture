using Aurora.Web.Integration.Feeds;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aurora.Tests.Live;

/// <summary>
/// Calls the live feed endpoints. Each test runs only when its endpoint is configured through the environment, so the
/// normal test run never reaches an external service. The API key is read from the environment and is not printed.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveFeedTests
{
    private const string MetricUrlVariable = "AURORA_LIVE_METRIC_FEED_URL";
    private const string MetricKeyVariable = "AURORA_LIVE_METRIC_FEED_KEY";
    private const string VulnerabilityUrlVariable = "AURORA_LIVE_VULNERABILITY_FEED_URL";
    private const string VulnerabilityKeyVariable = "AURORA_LIVE_VULNERABILITY_FEED_KEY";
    private const int LiveTimeoutSeconds = 30;

    [SkippableFact]
    public async Task LiveMetricFeed_ReturnsAPayloadThatTheParserReads()
    {
        var url = Environment.GetEnvironmentVariable(MetricUrlVariable) ?? string.Empty;
        Skip.If(string.IsNullOrWhiteSpace(url), $"Set {MetricUrlVariable} to run the live metric feed check.");

        var endpoint = new FeedEndpointOptions { BaseUrl = url, ApiKey = Environment.GetEnvironmentVariable(MetricKeyVariable), TimeoutSeconds = LiveTimeoutSeconds };
        using var client = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(LiveTimeoutSeconds) };
        var adapter = new HttpMetricFeedAdapter(client, Options.Create(new ExternalFeedOptions { Metrics = endpoint }));

        var payload = await adapter.FetchAsync(CancellationToken.None);
        var parsed = MetricFeedParser.Parse(payload, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.True(parsed.Accepted.Count + parsed.Rejected.Count > 0, "The live metric feed returned no readings.");
    }

    [SkippableFact]
    public async Task LiveVulnerabilityFeed_ReturnsAPayloadThatTheParserReads()
    {
        var url = Environment.GetEnvironmentVariable(VulnerabilityUrlVariable) ?? string.Empty;
        Skip.If(string.IsNullOrWhiteSpace(url), $"Set {VulnerabilityUrlVariable} to run the live vulnerability feed check.");

        var endpoint = new FeedEndpointOptions { BaseUrl = url, ApiKey = Environment.GetEnvironmentVariable(VulnerabilityKeyVariable), TimeoutSeconds = LiveTimeoutSeconds };
        using var client = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(LiveTimeoutSeconds) };
        var adapter = new HttpVulnerabilityFeedAdapter(client, Options.Create(new ExternalFeedOptions { Vulnerabilities = endpoint }));

        var payload = await adapter.FetchAsync(CancellationToken.None);
        var parsed = VulnerabilityFeedParser.Parse(payload, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.True(parsed.Accepted.Count + parsed.Rejected.Count > 0, "The live vulnerability feed returned no advisories.");
    }
}

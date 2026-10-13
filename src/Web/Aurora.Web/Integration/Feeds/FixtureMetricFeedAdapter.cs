namespace Aurora.Web.Integration.Feeds;

/// <summary>
/// Reads a metric feed payload from a fixture file under Data/Feeds/Fixtures. It stands in for an external source in
/// development and in tests. No live network feed is exercised by this adapter.
/// </summary>
public sealed class FixtureMetricFeedAdapter(IWebHostEnvironment environment) : IExternalFeedAdapter
{
    public const string Key = "fixture-metrics";
    public const string FixtureFileName = "metrics-feed.json";

    public string SourceKey => Key;

    public Task<string> FetchAsync(CancellationToken ct)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "Feeds", "Fixtures", FixtureFileName);
        return File.ReadAllTextAsync(path, ct);
    }
}

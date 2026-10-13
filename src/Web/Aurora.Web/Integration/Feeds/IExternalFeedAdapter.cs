namespace Aurora.Web.Integration.Feeds;

/// <summary>The kinds of data an external feed can carry. A job only runs adapters of the kind it handles.</summary>
public enum FeedKind
{
    Metrics = 0,
    Vulnerabilities = 1,
}

/// <summary>
/// The adapter boundary for an external feed. An adapter only fetches a raw payload from its source. The ingestion code
/// parses and validates every payload the same way, whatever the source, so no adapter can bypass validation.
/// Each adapter is tested against recorded fixtures, and the HTTP adapters are also tested against a stub server.
/// Live endpoints are exercised by the opt-in live tests, which run only when their endpoint is configured.
/// </summary>
public interface IExternalFeedAdapter
{
    /// <summary>Stable identifier of the source. It is stored in audit records and in job payloads.</summary>
    string SourceKey { get; }

    FeedKind Kind { get; }

    Task<string> FetchAsync(CancellationToken ct);
}

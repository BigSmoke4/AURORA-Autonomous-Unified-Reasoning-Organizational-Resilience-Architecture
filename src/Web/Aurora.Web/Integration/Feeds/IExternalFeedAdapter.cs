namespace Aurora.Web.Integration.Feeds;

/// <summary>
/// The adapter boundary for an external feed. An adapter only fetches a raw payload from its source. The ingestion code
/// parses and validates every payload the same way, whatever the source, so no adapter can bypass validation.
/// Adapters are tested with recorded fixtures. A live adapter must implement this interface and pass the same fixtures.
/// </summary>
public interface IExternalFeedAdapter
{
    /// <summary>Stable identifier of the source. It is stored in audit records and in job payloads.</summary>
    string SourceKey { get; }

    Task<string> FetchAsync(CancellationToken ct);
}

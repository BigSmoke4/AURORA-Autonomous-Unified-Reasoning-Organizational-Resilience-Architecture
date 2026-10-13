using Microsoft.Extensions.Options;

namespace Aurora.Web.Integration.Feeds;

/// <summary>
/// Connection settings for the live feeds, bound from the "ExternalFeeds" section. An endpoint is live only when its
/// BaseUrl is set. Otherwise the fixture adapter for that feed is used. The API key comes from configuration or the
/// environment, never from source control, and it is never logged or returned by any endpoint.
/// </summary>
public sealed class ExternalFeedOptions
{
    public const string SectionName = "ExternalFeeds";

    public FeedEndpointOptions Metrics { get; set; } = new();

    public FeedEndpointOptions Vulnerabilities { get; set; } = new();
}

public sealed class FeedEndpointOptions
{
    public const int DefaultTimeoutSeconds = 15;
    public const string DefaultPath = "/";

    public string? BaseUrl { get; set; }

    public string Path { get; set; } = DefaultPath;

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}

/// <summary>Rejects settings that would send a feed API key over an unencrypted connection, or that cannot be used.</summary>
public sealed class ExternalFeedOptionsValidator : IValidateOptions<ExternalFeedOptions>
{
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 120;

    public ValidateOptionsResult Validate(string? name, ExternalFeedOptions options)
    {
        var failures = new List<string>();
        Validate("Metrics", options.Metrics, failures);
        Validate("Vulnerabilities", options.Vulnerabilities, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Validate(string name, FeedEndpointOptions endpoint, List<string> failures)
    {
        if (!endpoint.IsConfigured) return;

        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"ExternalFeeds:{name}:BaseUrl must be an absolute https URL.");
        }

        if (endpoint.TimeoutSeconds < MinTimeoutSeconds || endpoint.TimeoutSeconds > MaxTimeoutSeconds)
        {
            failures.Add($"ExternalFeeds:{name}:TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}.");
        }
    }
}

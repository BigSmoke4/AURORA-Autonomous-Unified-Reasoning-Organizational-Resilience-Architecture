namespace Aurora.Modules.AI.Application;

/// <summary>
/// Strongly typed AI configuration (spec section 92), bound from the "AI" section. The API key is
/// deliberately never logged or returned by any endpoint.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>"None" (deterministic narrative only) or "OpenAICompatible".</summary>
    public string Provider { get; set; } = "None";

    /// <summary>Base URL of an OpenAI-compatible chat completions API, for example https://api.openai.com/v1.</summary>
    public string? Endpoint { get; set; }

    public string? Model { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 20;

    public bool IsLlmProviderConfigured =>
        string.Equals(Provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);
}

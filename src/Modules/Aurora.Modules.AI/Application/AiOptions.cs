namespace Aurora.Modules.AI.Application;

/// <summary>
/// Strongly typed AI configuration (spec section 92), bound from the "AI" section. The API key is
/// deliberately never logged or returned by any endpoint.
/// </summary>
public static class AiProviderNames
{
    public const string None = "None";
    public const string OpenAiCompatible = "OpenAICompatible";
}

public sealed class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>"None" (deterministic narrative only) or "OpenAICompatible".</summary>
    public string Provider { get; set; } = AiProviderNames.None;

    /// <summary>Base URL of an OpenAI-compatible chat completions API, for example https://api.openai.com/v1.</summary>
    public string? Endpoint { get; set; }

    public string? Model { get; set; }

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>How long an identical explanation is reused before the model is called again. Zero disables the cache.</summary>
    public int CacheMinutes { get; set; } = 60;

    public bool IsLlmProviderConfigured =>
        string.Equals(Provider, AiProviderNames.OpenAiCompatible, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);
}

using Aurora.Modules.AI.Application;
using Microsoft.Extensions.Options;

namespace Aurora.Web.Infrastructure.Ai;

/// <summary>
/// Rejects AI settings that would send the API key over an unencrypted connection, or that cannot be used. Plain HTTP
/// is accepted only for a loopback host, where the key never leaves the machine.
/// </summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 120;
    public const int MinCacheMinutes = 0;
    public const int MaxCacheMinutes = 1440;

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();
        var isOpenAiCompatible = string.Equals(options.Provider, AiProviderNames.OpenAiCompatible, StringComparison.OrdinalIgnoreCase);

        if (!isOpenAiCompatible && !string.Equals(options.Provider, AiProviderNames.None, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"AI:Provider must be '{AiProviderNames.None}' or '{AiProviderNames.OpenAiCompatible}'.");
        }

        if (isOpenAiCompatible && !options.IsLlmProviderConfigured)
        {
            failures.Add("AI:Provider OpenAICompatible requires AI:Endpoint, AI:Model and AI:ApiKey.");
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint) && !IsSecureOrLoopback(options.Endpoint))
        {
            failures.Add("AI:Endpoint must be an absolute https URL, or http on a loopback host.");
        }

        if (options.TimeoutSeconds < MinTimeoutSeconds || options.TimeoutSeconds > MaxTimeoutSeconds)
        {
            failures.Add($"AI:TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}.");
        }

        if (options.CacheMinutes < MinCacheMinutes || options.CacheMinutes > MaxCacheMinutes)
        {
            failures.Add($"AI:CacheMinutes must be between {MinCacheMinutes} and {MaxCacheMinutes}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsSecureOrLoopback(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }
}

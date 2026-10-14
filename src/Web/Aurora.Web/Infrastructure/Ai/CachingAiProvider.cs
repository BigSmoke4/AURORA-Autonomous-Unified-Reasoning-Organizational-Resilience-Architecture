using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.Modules.AI.Application;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Aurora.Web.Infrastructure.Ai;

/// <summary>
/// Caches model narratives in the shared cache (Redis when configured), so repeated identical evidence does not call
/// the model again. The key is a hash of the organization, the model identity, the system prompt and the evidence, so
/// a changed input never returns an old narrative, and no tenant can read or probe another tenant's entries. A cache failure is logged and the model is called directly, so an
/// unavailable cache slows a request but does not fail it. Only successful narratives are cached.
/// </summary>
public sealed class CachingAiProvider(IAiProvider inner, IDistributedCache cache, TimeSpan timeToLive, ILogger<CachingAiProvider> logger) : IAiProvider
{
    public const string KeyPrefix = "ai-narrative:v1:";

    private const char KeySeparator = '\u001F';

    public bool IsConfigured => inner.IsConfigured;

    public string ModelVersion => inner.ModelVersion;

    public async Task<NarrativeResult?> TryExplainAsync(Guid organizationId, string systemPrompt, string evidenceJson, CancellationToken ct = default)
    {
        if (timeToLive <= TimeSpan.Zero) return await inner.TryExplainAsync(organizationId, systemPrompt, evidenceJson, ct);

        var key = KeyPrefix + Hash(organizationId.ToString("N"), ModelVersion, systemPrompt, evidenceJson);
        var cached = await ReadAsync(key, ct);
        if (cached is not null) return cached;

        var result = await inner.TryExplainAsync(organizationId, systemPrompt, evidenceJson, ct);
        if (result is not null) await WriteAsync(key, result, ct);
        return result;
    }

    private async Task<NarrativeResult?> ReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var json = await cache.GetStringAsync(key, ct);
            return json is null ? null : JsonSerializer.Deserialize<NarrativeResult>(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The narrative cache could not be read. The model will be called instead.");
            return null;
        }
    }

    private async Task WriteAsync(string key, NarrativeResult result, CancellationToken ct)
    {
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The narrative cache could not be written. The narrative was returned without caching.");
        }
    }

    private static string Hash(params string[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(KeySeparator, parts))));
}

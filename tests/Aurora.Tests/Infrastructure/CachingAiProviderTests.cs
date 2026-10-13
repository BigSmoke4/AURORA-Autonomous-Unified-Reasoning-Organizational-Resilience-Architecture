using Aurora.Modules.AI.Application;
using Aurora.Web.Infrastructure.Ai;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aurora.Tests.Infrastructure;

public sealed class CachingAiProviderTests
{
    private const string SystemPrompt = "Explain the evidence without inventing numbers.";
    private const string Evidence = "{\"metric\":\"cycle time\",\"value\":14.2}";
    private const string OtherEvidence = "{\"metric\":\"cycle time\",\"value\":15.0}";
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Fact]
    public async Task AnIdenticalRequest_IsAnsweredFromTheCache_WithoutCallingTheModelAgain()
    {
        var inner = new CountingProvider(new NarrativeResult("Cycle time is within the window.", "model-a"));
        var provider = CreateProvider(inner, new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), OneHour);

        var first = await provider.TryExplainAsync(SystemPrompt, Evidence);
        var second = await provider.TryExplainAsync(SystemPrompt, Evidence);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ChangedEvidence_IsNeverAnsweredWithAnOldNarrative()
    {
        var inner = new CountingProvider(new NarrativeResult("Text.", "model-a"));
        var provider = CreateProvider(inner, new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), OneHour);

        await provider.TryExplainAsync(SystemPrompt, Evidence);
        await provider.TryExplainAsync(SystemPrompt, OtherEvidence);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task AFailedAnswer_IsNotCached()
    {
        var inner = new CountingProvider(null);
        var provider = CreateProvider(inner, new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), OneHour);

        Assert.Null(await provider.TryExplainAsync(SystemPrompt, Evidence));
        Assert.Null(await provider.TryExplainAsync(SystemPrompt, Evidence));

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task ZeroTimeToLive_DisablesTheCache()
    {
        var inner = new CountingProvider(new NarrativeResult("Text.", "model-a"));
        var provider = CreateProvider(inner, new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), TimeSpan.Zero);

        await provider.TryExplainAsync(SystemPrompt, Evidence);
        await provider.TryExplainAsync(SystemPrompt, Evidence);

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task AnUnavailableCache_DoesNotFailTheRequest()
    {
        var expected = new NarrativeResult("Text from the model.", "model-a");
        var inner = new CountingProvider(expected);
        var provider = CreateProvider(inner, new FailingCache(), OneHour);

        var result = await provider.TryExplainAsync(SystemPrompt, Evidence);

        Assert.Equal(expected, result);
        Assert.Equal(1, inner.Calls);
    }

    private static CachingAiProvider CreateProvider(IAiProvider inner, IDistributedCache cache, TimeSpan timeToLive) =>
        new(inner, cache, timeToLive, NullLogger<CachingAiProvider>.Instance);

    private sealed class CountingProvider(NarrativeResult? result) : IAiProvider
    {
        public int Calls { get; private set; }

        public bool IsConfigured => true;

        public string ModelVersion => "model-a";

        public Task<NarrativeResult?> TryExplainAsync(string systemPrompt, string evidenceJson, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FailingCache : IDistributedCache
    {
        private static InvalidOperationException Unavailable() => new("The cache is unavailable.");

        public byte[]? Get(string key) => throw Unavailable();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Unavailable();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Unavailable();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw Unavailable();

        public void Refresh(string key) => throw Unavailable();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Unavailable();

        public void Remove(string key) => throw Unavailable();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Unavailable();
    }
}

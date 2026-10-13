using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Xunit;

namespace Aurora.Tests.Infrastructure;

/// <summary>
/// Checks that two application instances share one Redis cache. It runs only when AURORA_TEST_REDIS names a Redis
/// server, and CI starts one for this purpose.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RedisCacheIntegrationTests
{
    private const string ConnectionVariable = "AURORA_TEST_REDIS";
    private const string InstanceName = "aurora-test:";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    [SkippableFact]
    public async Task TwoInstances_ShareOneNarrativeThroughRedis()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable) ?? string.Empty;
        Skip.If(string.IsNullOrWhiteSpace(connection), $"Set {ConnectionVariable} to run the Redis check.");

        var key = "shared-check:" + Guid.NewGuid().ToString("N");
        using var writer = new RedisCache(new RedisCacheOptions { Configuration = connection, InstanceName = InstanceName });
        using var reader = new RedisCache(new RedisCacheOptions { Configuration = connection, InstanceName = InstanceName });

        await writer.SetStringAsync(key, "{\"text\":\"shared\"}", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime });
        var read = await reader.GetStringAsync(key);
        await writer.RemoveAsync(key);

        Assert.Equal("{\"text\":\"shared\"}", read);
    }
}

using Aurora.Tests.Persistence;
using Aurora.Web.Integration.Feeds;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aurora.Tests.Hosting;

/// <summary>
/// Resolves the real feed registrations from the started host. Each feed must have exactly one adapter: the fixture
/// when no endpoint is configured, and the live adapter when one is. Two adapters for the same feed would let the
/// fixture silently win, so this test checks the count as well as the type.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class FeedAdapterWiringTests
{
    private const string LiveBaseUrl = "https://feeds.example.test";

    [Fact]
    public void WithoutEndpoints_EachFeedHasExactlyOneFixtureAdapter()
    {
        using var factory = CreateFactory(liveFeeds: false);
        using var scope = factory.Services.CreateScope();

        var adapters = scope.ServiceProvider.GetServices<IExternalFeedAdapter>().ToList();

        Assert.Equal(2, adapters.Count);
        Assert.IsType<FixtureMetricFeedAdapter>(Single(adapters, FeedKind.Metrics));
        Assert.IsType<FixtureVulnerabilityFeedAdapter>(Single(adapters, FeedKind.Vulnerabilities));
    }

    [Fact]
    public void WithEndpoints_EachFeedHasExactlyOneLiveAdapter()
    {
        using var factory = CreateFactory(liveFeeds: true);
        using var scope = factory.Services.CreateScope();

        var adapters = scope.ServiceProvider.GetServices<IExternalFeedAdapter>().ToList();

        Assert.Equal(2, adapters.Count);
        Assert.IsType<HttpMetricFeedAdapter>(Single(adapters, FeedKind.Metrics));
        Assert.IsType<HttpVulnerabilityFeedAdapter>(Single(adapters, FeedKind.Vulnerabilities));
    }

    private static IExternalFeedAdapter Single(IReadOnlyList<IExternalFeedAdapter> adapters, FeedKind kind)
    {
        var matches = adapters.Where(a => a.Kind == kind).ToList();
        Assert.Single(matches);
        return matches[0];
    }

    private static WebApplicationFactory<Program> CreateFactory(bool liveFeeds) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:AuroraDb", TestDatabase.ReadDisposableDatabaseConnection());
            builder.UseSetting("Security:RequireHttps", "false");
            if (liveFeeds)
            {
                builder.UseSetting("ExternalFeeds:Metrics:BaseUrl", LiveBaseUrl);
                builder.UseSetting("ExternalFeeds:Vulnerabilities:BaseUrl", LiveBaseUrl);
            }
        });
}

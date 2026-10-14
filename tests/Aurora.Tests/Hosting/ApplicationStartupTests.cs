using System.Net;
using Microsoft.AspNetCore.Hosting;
using Aurora.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Aurora.Tests.Hosting;

/// <summary>
/// Starts the real web host in Production mode against PostgreSQL. Host startup runs option validation
/// (including the AI resilience settings), seeding, and migrations, so a misconfiguration fails here
/// instead of only at deployment.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ApplicationStartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CspHeader = "Content-Security-Policy";

    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationStartupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:AuroraDb", TestDatabase.ReadDisposableDatabaseConnection());
            builder.UseSetting("Security:RequireHttps", "false");
        });
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/Account/Login")]
    public async Task Host_StartsAndServes(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Pages_CarryTheContentSecurityPolicy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.True(response.Headers.Contains(CspHeader));
        Assert.Contains("script-src 'self'", string.Join(';', response.Headers.GetValues(CspHeader)));
    }
}

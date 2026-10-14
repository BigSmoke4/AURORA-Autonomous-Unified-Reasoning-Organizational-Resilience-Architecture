using System.Net;
using System.Net.Http.Headers;
using Aurora.Web.Integration.Feeds;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aurora.Tests.Feeds;

/// <summary>
/// Exercises the live HTTP adapters against a stub server, so the request, the authentication header, the failure
/// handling and the size limit are all checked without a network connection.
/// </summary>
public sealed class HttpFeedAdapterTests
{
    private const string BaseUrl = "https://feeds.example.test/v1";
    private const string ApiKey = "stub-api-key-value";
    private const string MetricPath = "metrics";

    [Fact]
    public async Task Success_ReturnsThePayload_AndSendsTheKeyOnlyAsABearerHeader()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FeedFixtureFile.Read(FixtureMetricFeedAdapter.FixtureFileName)),
        });
        var adapter = CreateMetricAdapter(handler);

        var body = await adapter.FetchAsync(CancellationToken.None);

        Assert.Equal(FeedFixtureFile.Read(FixtureMetricFeedAdapter.FixtureFileName), body);
        Assert.Equal($"{BaseUrl}/{MetricPath}", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", ApiKey), handler.LastRequest.Headers.Authorization);
    }

    [Fact]
    public async Task AFailedStatus_ThrowsWithTheStatusCode_AndNeverTheKey()
    {
        var adapter = CreateMetricAdapter(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("The key " + ApiKey + " was refused."),
        }));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => adapter.FetchAsync(CancellationToken.None));

        Assert.Contains("401", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, error.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task APayloadOverTheSizeLimit_IsRejected()
    {
        var oversized = new string('a', HttpFeedClient.MaxPayloadBytes + 1);
        var adapter = CreateMetricAdapter(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(oversized),
        }));

        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.FetchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnUnconfiguredEndpoint_FailsBeforeAnyRequestIsSent()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = new HttpMetricFeedAdapter(new HttpClient(handler), Options.Create(new ExternalFeedOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.FetchAsync(CancellationToken.None));
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task TheVulnerabilityAdapter_ReturnsItsPayloadAndDeclaresItsKind()
    {
        var body = FeedFixtureFile.Read(FixtureVulnerabilityFeedAdapter.FixtureFileName);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var options = Options.Create(new ExternalFeedOptions
        {
            Vulnerabilities = new FeedEndpointOptions { BaseUrl = BaseUrl, Path = "advisories", ApiKey = ApiKey },
        });
        var adapter = new HttpVulnerabilityFeedAdapter(CreateClient(handler), options);

        Assert.Equal(FeedKind.Vulnerabilities, adapter.Kind);
        Assert.Equal(body, await adapter.FetchAsync(CancellationToken.None));
    }

    private static HttpMetricFeedAdapter CreateMetricAdapter(StubHandler handler) =>
        new(CreateClient(handler), Options.Create(new ExternalFeedOptions
        {
            Metrics = new FeedEndpointOptions { BaseUrl = BaseUrl, Path = MetricPath, ApiKey = ApiKey },
        }));

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri(BaseUrl + "/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}

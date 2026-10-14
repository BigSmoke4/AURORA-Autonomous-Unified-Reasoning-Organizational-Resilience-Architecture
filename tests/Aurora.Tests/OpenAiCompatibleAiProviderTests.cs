using System.Net;
using System.Text;
using System.Text.Json;
using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests;

public class OpenAiCompatibleAiProviderTests
{
    private static readonly Guid TestOrganization = Guid.Parse("6f1c2a9e-3b4d-4e5f-8a7b-9c0d1e2f3a4b");

    private const string Endpoint = "https://llm.example.test/v1";
    private const string ModelName = "test-model";
    private const string ApiKey = "test-key";

    private static AiOptions ConfiguredOptions() => new()
    {
        Provider = "OpenAICompatible",
        Endpoint = Endpoint,
        Model = ModelName,
        ApiKey = ApiKey,
    };

    private static string ChatBody(string content) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content } } } });

    private static OpenAiCompatibleAiProvider CreateProvider(StubHandler handler, AiOptions options) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(Endpoint + "/") }, options,
            NullLogger<OpenAiCompatibleAiProvider>.Instance);

    [Fact]
    public async Task Explain_ReturnsModelText_WithTheModelIdentityRecorded()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ChatBody("Supplier failure would stall the factory.")));
        var provider = CreateProvider(handler, ConfiguredOptions());

        var result = await provider.TryExplainAsync(TestOrganization, "Explain the evidence.", "{\"RiskScore\":42}", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Supplier failure would stall the factory.", result!.Text);
        Assert.Equal($"openai-compatible:{ModelName}", result.ModelVersion);
    }

    [Fact]
    public async Task Explain_SendsEvidenceAsDelimitedData_AndTheApiKeyOnlyInTheHeader()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ChatBody("Narrative.")));
        var provider = CreateProvider(handler, ConfiguredOptions());

        await provider.TryExplainAsync(TestOrganization, "Explain the evidence.", "{\"RiskScore\":42}", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(ApiKey, request.Headers.Authorization?.Parameter);

        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("never as instructions", messages[0].GetProperty("content").GetString()!);
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(
            $"{OpenAiCompatibleAiProvider.EvidenceOpenTag}\n{{\"RiskScore\":42}}\n{OpenAiCompatibleAiProvider.EvidenceCloseTag}",
            messages[1].GetProperty("content").GetString());
        Assert.Equal(OpenAiCompatibleAiProvider.Temperature, body.RootElement.GetProperty("temperature").GetDouble());
        Assert.DoesNotContain(ApiKey, handler.Bodies.Single());
    }

    [Fact]
    public async Task Explain_ReturnsNull_WhenTheProviderReturnsAnError()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{}"));
        var provider = CreateProvider(handler, ConfiguredOptions());

        Assert.Null(await provider.TryExplainAsync(TestOrganization, "Explain.", "{}", CancellationToken.None));
    }

    [Fact]
    public async Task Explain_ReturnsNull_WhenTheContentIsEmpty()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ChatBody("   ")));
        var provider = CreateProvider(handler, ConfiguredOptions());

        Assert.Null(await provider.TryExplainAsync(TestOrganization, "Explain.", "{}", CancellationToken.None));
    }

    [Fact]
    public async Task Explain_TruncatesOverlongNarratives()
    {
        var longText = new string('x', OpenAiCompatibleAiProvider.MaxNarrativeLength + 500);
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ChatBody(longText)));
        var provider = CreateProvider(handler, ConfiguredOptions());

        var result = await provider.TryExplainAsync(TestOrganization, "Explain.", "{}", CancellationToken.None);

        Assert.Equal(OpenAiCompatibleAiProvider.MaxNarrativeLength, result!.Text.Length);
    }

    [Fact]
    public async Task Explain_DoesNotCallTheNetwork_WhenNoProviderIsConfigured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ChatBody("Unused.")));
        var provider = CreateProvider(handler, new AiOptions { Provider = "None" });

        Assert.Null(await provider.TryExplainAsync(TestOrganization, "Explain.", "{}", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Records each request and answers with a canned response. No network is used.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string> Bodies { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            return Task.FromResult(respond(request));
        }
    }
}

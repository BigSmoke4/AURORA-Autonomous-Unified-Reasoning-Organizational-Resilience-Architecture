using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests.Live;

/// <summary>
/// Calls a live OpenAI-compatible model endpoint. It runs only when AURORA_LIVE_MODEL_ENDPOINT, AURORA_LIVE_MODEL_NAME and
/// AURORA_LIVE_MODEL_KEY are set. The key is read from the environment and is never printed.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveModelEndpointTests
{
    private static readonly Guid TestOrganization = Guid.Parse("6f1c2a9e-3b4d-4e5f-8a7b-9c0d1e2f3a4b");

    private const string EndpointVariable = "AURORA_LIVE_MODEL_ENDPOINT";
    private const string ModelVariable = "AURORA_LIVE_MODEL_NAME";
    private const string KeyVariable = "AURORA_LIVE_MODEL_KEY";
    private const int LiveTimeoutSeconds = 60;

    private const string SystemPrompt = "Explain the evidence in one sentence. Do not add any number that is not in the evidence.";
    private const string Evidence = "{\"metric\":\"cycle time\",\"valueSeconds\":14.2,\"thresholdSeconds\":30}";

    [SkippableFact]
    public async Task LiveEndpoint_ReturnsANarrativeAndNamesTheModel()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable) ?? string.Empty;
        var model = Environment.GetEnvironmentVariable(ModelVariable) ?? string.Empty;
        var key = Environment.GetEnvironmentVariable(KeyVariable) ?? string.Empty;
        Skip.If(string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(key),
            $"Set {EndpointVariable}, {ModelVariable} and {KeyVariable} to run the live model check.");

        var options = new AiOptions
        {
            Provider = "OpenAICompatible",
            Endpoint = endpoint,
            Model = model,
            ApiKey = key,
            TimeoutSeconds = LiveTimeoutSeconds,
        };
        using var client = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(LiveTimeoutSeconds) };
        var provider = new OpenAiCompatibleAiProvider(client, options, NullLogger<OpenAiCompatibleAiProvider>.Instance);

        var result = await provider.TryExplainAsync(TestOrganization, SystemPrompt, Evidence);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.Text));
        Assert.Equal(provider.ModelVersion, result.ModelVersion);
    }
}

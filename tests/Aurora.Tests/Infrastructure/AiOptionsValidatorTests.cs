using Aurora.Modules.AI.Application;
using Aurora.Web.Infrastructure.Ai;
using Xunit;

namespace Aurora.Tests.Infrastructure;

public sealed class AiOptionsValidatorTests
{
    private const string SecureEndpoint = "https://models.example.test/v1";
    private const string LoopbackEndpoint = "http://localhost:11434/v1";
    private const string PlainEndpoint = "http://models.example.test/v1";
    private static readonly AiOptionsValidator Validator = new();

    [Fact]
    public void TheDefaultNoneProvider_IsValid()
    {
        Assert.True(Validator.Validate(null, new AiOptions()).Succeeded);
    }

    [Fact]
    public void AConfiguredHttpsEndpoint_IsValid()
    {
        Assert.True(Validator.Validate(null, Configured(SecureEndpoint)).Succeeded);
    }

    [Fact]
    public void AConfiguredLoopbackEndpoint_IsValid_BecauseTheKeyNeverLeavesTheMachine()
    {
        Assert.True(Validator.Validate(null, Configured(LoopbackEndpoint)).Succeeded);
    }

    [Fact]
    public void APlainHttpEndpoint_IsRejected_SoTheKeyIsNeverSentInTheClear()
    {
        var result = Validator.Validate(null, Configured(PlainEndpoint));

        Assert.False(result.Succeeded);
        Assert.Contains((result.Failures ?? Array.Empty<string>()), failure => failure.Contains("AI:Endpoint must be an absolute https URL", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOpenAiProviderWithoutAKey_IsRejected()
    {
        var options = Configured(SecureEndpoint);
        options.ApiKey = null;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void AnUnknownProvider_IsRejected()
    {
        Assert.False(Validator.Validate(null, new AiOptions { Provider = "SomeOtherVendor" }).Succeeded);
    }

    [Fact]
    public void OutOfRangeTimeoutAndCache_AreRejected()
    {
        var tooLongTimeout = new AiOptions { TimeoutSeconds = AiOptionsValidator.MaxTimeoutSeconds + 1 };
        var negativeCache = new AiOptions { CacheMinutes = AiOptionsValidator.MinCacheMinutes - 1 };

        Assert.False(Validator.Validate(null, tooLongTimeout).Succeeded);
        Assert.False(Validator.Validate(null, negativeCache).Succeeded);
    }

    private static AiOptions Configured(string endpoint) => new()
    {
        Provider = AiProviderNames.OpenAiCompatible,
        Endpoint = endpoint,
        Model = "test-model",
        ApiKey = "test-key-not-real",
    };
}

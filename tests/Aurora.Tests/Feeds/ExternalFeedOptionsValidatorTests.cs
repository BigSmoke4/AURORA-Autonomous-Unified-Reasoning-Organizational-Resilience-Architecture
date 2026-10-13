using Aurora.Web.Integration.Feeds;
using Xunit;

namespace Aurora.Tests.Feeds;

public sealed class ExternalFeedOptionsValidatorTests
{
    private static readonly ExternalFeedOptionsValidator Validator = new();

    [Fact]
    public void UnconfiguredEndpoints_AreValid()
    {
        Assert.True(Validator.Validate(null, new ExternalFeedOptions()).Succeeded);
    }

    [Fact]
    public void AnHttpsEndpointWithAValidTimeout_IsValid()
    {
        var options = new ExternalFeedOptions
        {
            Metrics = new FeedEndpointOptions { BaseUrl = "https://feeds.example.test", TimeoutSeconds = 15 },
        };

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void AnUnencryptedEndpoint_IsRejected_SoAKeyIsNeverSentInTheClear()
    {
        var options = new ExternalFeedOptions
        {
            Vulnerabilities = new FeedEndpointOptions { BaseUrl = "http://feeds.example.test", ApiKey = "stub-api-key-value" },
        };

        var result = Validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Contains("https", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Failures, f => f.Contains("stub-api-key-value", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void ATimeoutOutsideTheAllowedRange_IsRejected(int seconds)
    {
        var options = new ExternalFeedOptions
        {
            Metrics = new FeedEndpointOptions { BaseUrl = "https://feeds.example.test", TimeoutSeconds = seconds },
        };

        Assert.False(Validator.Validate(null, options).Succeeded);
    }
}

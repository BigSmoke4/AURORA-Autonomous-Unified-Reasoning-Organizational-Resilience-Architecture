using Aurora.Web.Hubs;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Aurora.Tests.Infrastructure;

public sealed class HubOriginPolicyTests
{
    private const string Scheme = "https";
    private const string ApplicationHost = "aurora.example.test";
    private const string ApplicationOrigin = "https://aurora.example.test";
    private const string ForeignOrigin = "https://attacker.example";
    private static readonly HostString Host = new(ApplicationHost);

    [Fact]
    public void TheApplicationsOwnOrigin_IsAllowed()
    {
        Assert.True(HubOriginPolicy.IsAllowed(ApplicationOrigin, Scheme, Host, []));
    }

    [Fact]
    public void AForeignOrigin_IsRefused_SoAPageOnAnotherSiteCannotOpenASocketAsTheUser()
    {
        Assert.False(HubOriginPolicy.IsAllowed(ForeignOrigin, Scheme, Host, []));
    }

    [Fact]
    public void AMissingOrigin_IsRefused()
    {
        Assert.False(HubOriginPolicy.IsAllowed(null, Scheme, Host, []));
        Assert.False(HubOriginPolicy.IsAllowed("  ", Scheme, Host, []));
    }

    [Fact]
    public void ASameHostOnAnotherScheme_IsRefused()
    {
        Assert.False(HubOriginPolicy.IsAllowed("http://aurora.example.test", Scheme, Host, []));
    }

    [Fact]
    public void AnExplicitlyConfiguredOrigin_IsAllowed_WithOrWithoutATrailingSlash()
    {
        var allowed = new[] { "https://dashboard.example.test/" };

        Assert.True(HubOriginPolicy.IsAllowed("https://dashboard.example.test", Scheme, Host, allowed));
        Assert.True(HubOriginPolicy.IsAllowed("HTTPS://Dashboard.Example.Test/", Scheme, Host, allowed));
    }

    [Fact]
    public void APortIsPartOfTheOrigin()
    {
        var host = new HostString("localhost:5001");

        Assert.True(HubOriginPolicy.IsAllowed("https://localhost:5001", Scheme, host, []));
        Assert.False(HubOriginPolicy.IsAllowed("https://localhost:6666", Scheme, host, []));
    }
}

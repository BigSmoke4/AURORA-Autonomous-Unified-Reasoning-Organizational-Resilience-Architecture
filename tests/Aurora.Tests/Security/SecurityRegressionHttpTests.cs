using System.Net;
using System.Text.RegularExpressions;
using Aurora.Modules.Identity.Domain;
using Aurora.Tests.Persistence;
using Aurora.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aurora.Tests.Security;

/// <summary>
/// Regression checks for the controls in docs/SECURITY-REVIEW.md. Each test starts its own host, because lockout and the
/// sign-in rate limit are held in process memory, and a test must not see another test's failed attempts.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class SecurityRegressionHttpTests
{
    private const string Password = "Aurora-Test-Password-1!";
    private const string DemoOrganizationName = "Meridian Industrial Group";
    private const string LoginPath = "/Account/Login";
    private const string LogoutPath = "/Account/Logout";
    private const string OrganizationHubNegotiatePath = "/hubs/organization/negotiate?negotiateVersion=1";
    private const string MissingPagePath = "/this-page-does-not-exist";
    private const string AntiforgeryField = "__RequestVerificationToken";
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
    private const string ContentTypeOptionsHeader = "X-Content-Type-Options";
    private const string NoSniff = "nosniff";
    private const string BaseAddress = "https://localhost";
    private const string UnknownEmail = "nobody-registered@example.com";
    private const int FailedAttemptsBeforeLockout = 5;
    private const int SignInAttemptsPerMinute = 10;
    private const string StackTraceMarker = "   at ";
    private const string ExceptionMarker = "Exception";
    private static readonly Regex TokenPattern = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public async Task APostWithoutAnAntiforgeryToken_IsRejected()
    {
        using var host = CreateHost();
        using var client = CreateClient(host);

        var response = await client.PostAsync(LogoutPath, new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AHubConnection_RequiresASignedInUser()
    {
        using var host = CreateHost();
        using var client = CreateClient(host);

        var response = await client.PostAsync(OrganizationHubNegotiatePath, new StringContent(string.Empty));

        var redirectedToSignIn = response.StatusCode == HttpStatusCode.Redirect
            && (response.Headers.Location?.OriginalString ?? string.Empty).Contains(LoginPath, StringComparison.Ordinal);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || redirectedToSignIn,
            $"The hub negotiation returned {(int)response.StatusCode} to an anonymous caller.");
    }

    [Fact]
    public async Task AMissingPage_CarriesTheSecurityHeaders_AndRevealsNoDetails()
    {
        using var host = CreateHost();
        using var client = CreateClient(host);

        var response = await client.GetAsync(MissingPagePath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains(ContentSecurityPolicyHeader), "The error page has no Content-Security-Policy.");
        Assert.Equal(NoSniff, string.Join(',', response.Headers.GetValues(ContentTypeOptionsHeader)));
        Assert.DoesNotContain(StackTraceMarker, body, StringComparison.Ordinal);
        Assert.DoesNotContain(ExceptionMarker, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAccount_IsLockedAfterTooManyFailedSignIns_EvenWithTheCorrectPassword()
    {
        using var host = CreateHost();
        using var client = CreateClient(host);
        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        await CreateUserAsync(host, email);
        var token = await ReadAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < FailedAttemptsBeforeLockout; attempt++)
        {
            var failed = await PostLoginAsync(client, token, email, "wrong-password-" + attempt);
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        }

        var correct = await PostLoginAsync(client, token, email, Password);

        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
    }

    [Fact]
    public async Task SignIn_IsRateLimitedPerClientAddress()
    {
        using var host = CreateHost();
        using var client = CreateClient(host);
        var token = await ReadAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < SignInAttemptsPerMinute; attempt++)
        {
            var response = await PostLoginAsync(client, token, UnknownEmail, Password);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limited = await PostLoginAsync(client, token, UnknownEmail, Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:AuroraDb", TestDatabase.ReadDisposableDatabaseConnection());
            builder.UseSetting("Security:RequireHttps", "false");
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(BaseAddress),
            AllowAutoRedirect = false,
        });

    private static async Task CreateUserAsync(WebApplicationFactory<Program> host, string email)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var organizationId = await db.Organizations.AsNoTracking()
            .Where(o => o.Name == DemoOrganizationName)
            .Select(o => o.Id)
            .SingleAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Security Test User",
            OrganizationId = organizationId,
            IsActive = true,
            EmailConfirmed = true,
        };
        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(HttpClient client)
    {
        var html = await (await client.GetAsync(LoginPath)).Content.ReadAsStringAsync();
        var match = TokenPattern.Match(html);
        Assert.True(match.Success, "The sign-in page has no antiforgery token.");
        return match.Groups[1].Value;
    }

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string token, string email, string password) =>
        client.PostAsync(LoginPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            [AntiforgeryField] = token,
        }));
}

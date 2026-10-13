using System.Net;
using System.Text.RegularExpressions;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Shared.Kernel;
using Aurora.Tests.Persistence;
using Aurora.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aurora.Tests.Web;

/// <summary>
/// HTTP-level checks of the operational module pages, run through the real host against PostgreSQL. They sign in
/// through the login form, so the authentication, antiforgery, permission, and tenant rules are exercised together.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class ModulePagesHttpTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string Password = "Aurora-Test-Password-1!";
    private const string DemoOrganizationName = "Meridian Industrial Group";
    private const string IsolatedOrganizationName = "Module HTTP Isolated Org";
    private const string AdministratorEmail = "module-admin@example.com";
    private const string IsolatedEmail = "module-isolated@example.com";
    private const string DeactivatedEmail = "module-deactivated@example.com";
    private const string LoginPath = "/Account/Login";
    private const string WorkforcePath = "/Workforce";
    private const string AntiforgeryField = "__RequestVerificationToken";
    private const string InvalidLoginMessage = "Invalid login attempt.";
    private const string EmptyWorkforceMessage = "No critical roles are defined yet.";
    private const string CsvContentType = "text/csv";
    private const string CsvHeader = "Area,Metric,Value,Status\r\n";
    private const string CsvByteOrderMark = "\uFEFF";
    private const string NewRoleName = "HTTP Test Role";
    private const string NewRoleTitle = "HTTP Test Title";

    // Secure auth cookies are only sent back over HTTPS, so the test client uses an https base address.
    private const string BaseAddress = "https://localhost";

    private static readonly (string Path, string Marker)[] ModulePages =
    {
        ("/Workforce", "Plant operations continuity"),
        ("/Finance", "PRD-100"),
        ("/Operations", "Factory Alpha"),
        ("/Technology", "MES Production App"),
        ("/Markets", "Industrial actuators"),
        ("/Assets", "CNC machining centre"),
        ("/Contracts", "NB-2025-07"),
        ("/SupplyChain", "IAC-7000"),
        ("/Monitoring", "Line 1 cycle time"),
        ("/Alerts", "Line 1 cycle time above maximum"),
        ("/Customers", "Continental Heavy Equipment"),
        ("/Reporting", "Plant operations continuity"),
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ModulePagesHttpTests(WebApplicationFactory<Program> factory)
    {
        var connectionString = TestDatabase.ReadDisposableDatabaseConnection();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:AuroraDb", connectionString);
            builder.UseSetting("Security:RequireHttps", "false");
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();

        // The first request is served only after startup has migrated and seeded the demo enterprise.
        using (var warmup = CreateClient())
        {
            var health = await warmup.GetAsync("/health/live");
            Assert.True(health.StatusCode == HttpStatusCode.OK, "The host did not start.");
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var demo = await db.Organizations.AsNoTracking().SingleAsync(o => o.Name == DemoOrganizationName);
        var isolated = new Organization(IsolatedOrganizationName, "Testing");
        db.Organizations.Add(isolated);
        await db.SaveChangesAsync();

        await CreateUserAsync(userManager, AdministratorEmail, demo.Id, isActive: true);
        await CreateUserAsync(userManager, IsolatedEmail, isolated.Id, isActive: true);
        await CreateUserAsync(userManager, DeactivatedEmail, demo.Id, isActive: false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/Workforce")]
    [InlineData("/Finance")]
    [InlineData("/Operations")]
    [InlineData("/Technology")]
    [InlineData("/Markets")]
    [InlineData("/Assets")]
    [InlineData("/Contracts")]
    [InlineData("/SupplyChain")]
    [InlineData("/Monitoring")]
    [InlineData("/Alerts")]
    [InlineData("/Customers")]
    [InlineData("/Reporting")]
    [InlineData("/Reporting/Export")]
    public async Task ModulePages_RedirectAnonymousUsersToSignIn(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(LoginPath, response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Fact]
    public async Task DeactivatedUser_CannotSignIn()
    {
        using var client = CreateClient();
        var token = ReadAntiforgeryToken(await GetTextAsync(client, LoginPath));

        var response = await client.PostAsync(LoginPath, Form(new Dictionary<string, string>
        {
            ["Email"] = DeactivatedEmail,
            ["Password"] = Password,
            [AntiforgeryField] = token,
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(InvalidLoginMessage, await response.Content.ReadAsStringAsync());

        var page = await client.GetAsync(WorkforcePath);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains(LoginPath, page.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Fact]
    public async Task Administrator_SeesEveryModulePage_WithSeededData()
    {
        using var client = await SignInAsync(AdministratorEmail);

        foreach (var (path, marker) in ModulePages)
        {
            var html = await GetTextAsync(client, path);
            Assert.True(html.Contains(marker, StringComparison.Ordinal), $"{path} does not show '{marker}'.");
        }
    }

    [Fact]
    public async Task RecordedRole_IsVisibleToItsOwnTenantOnly()
    {
        using var administrator = await SignInAsync(AdministratorEmail);
        var token = ReadAntiforgeryToken(await GetTextAsync(administrator, WorkforcePath));

        var response = await administrator.PostAsync("/Workforce/Create", Form(new Dictionary<string, string>
        {
            ["Name"] = NewRoleName,
            ["RoleTitle"] = NewRoleTitle,
            ["RequiredHeadcount"] = "1",
            ["Criticality"] = "3",
            [AntiforgeryField] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(WorkforcePath, response.Headers.Location?.OriginalString ?? string.Empty);
        Assert.Contains(NewRoleName, await GetTextAsync(administrator, WorkforcePath));

        using var isolated = await SignInAsync(IsolatedEmail);
        var otherTenant = await GetTextAsync(isolated, WorkforcePath);
        Assert.DoesNotContain(NewRoleName, otherTenant);
        Assert.Contains(EmptyWorkforceMessage, otherTenant);
    }

    [Fact]
    public async Task ExecutiveReport_ExportsCsvForAnAuthorizedUser()
    {
        using var administrator = await SignInAsync(AdministratorEmail);

        var response = await administrator.GetAsync("/Reporting/Export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CsvContentType, response.Content.Headers.ContentType?.MediaType);
        var csv = (await response.Content.ReadAsStringAsync()).TrimStart(CsvByteOrderMark[0]);
        Assert.StartsWith(CsvHeader, csv, StringComparison.Ordinal);
        Assert.Contains("Plant operations continuity", csv, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri(BaseAddress),
        AllowAutoRedirect = false,
    });

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = CreateClient();
        var token = ReadAntiforgeryToken(await GetTextAsync(client, LoginPath));

        var response = await client.PostAsync(LoginPath, Form(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            [AntiforgeryField] = token,
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Sign-in for {email} returned {(int)response.StatusCode}.");
        return client;
    }

    private static async Task<string> GetTextAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync();
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
        {
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        }

        Assert.True(match.Success, "The page did not include an antiforgery token.");
        return match.Groups[1].Value;
    }

    private static FormUrlEncodedContent Form(IReadOnlyDictionary<string, string> fields) => new(fields);

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager, string email, Guid organizationId, bool isActive)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Module Test User",
            OrganizationId = organizationId,
            IsActive = isActive,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        var granted = await userManager.AddToRoleAsync(user, AuroraRoles.SystemAdministrator);
        Assert.True(granted.Succeeded, string.Join("; ", granted.Errors.Select(e => e.Description)));
    }

    private static async Task ResetDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AuroraDbContext>()
            .UseNpgsql(TestDatabase.ReadDisposableDatabaseConnection())
            .Options;

        await using var db = new AuroraDbContext(options, new NoTenant());
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

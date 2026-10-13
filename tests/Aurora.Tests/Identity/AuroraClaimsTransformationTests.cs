using System.Security.Claims;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Shared.Kernel;
using Aurora.Tests.Persistence;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aurora.Tests.Identity;

/// <summary>
/// Tenant claim resolution against real ASP.NET Core Identity and PostgreSQL. These tests cover the
/// two rules that decide which organization a request may touch: an active user gets their home
/// organization, and a deactivated or deleted user gets none.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class AuroraClaimsTransformationTests
{
    private const string Password = "Aurora-Test-Password-1!";

    [Fact]
    public async Task ActiveUserWithOrganization_GetsTenantClaim()
    {
        await using var provider = await PrepareDatabaseAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var organization = await AddOrganizationAsync(db);
        var user = await CreateUserAsync(userManager, organization.Id, isActive: true);

        var result = await new AuroraClaimsTransformation(userManager).TransformAsync(PrincipalFor(user.Id));

        Assert.Equal(organization.Id.ToString(), result.FindFirstValue(AuroraClaims.OrganizationId));
    }

    [Fact]
    public async Task DeactivatedUser_GetsNoTenantClaim()
    {
        await using var provider = await PrepareDatabaseAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var organization = await AddOrganizationAsync(db);
        var user = await CreateUserAsync(userManager, organization.Id, isActive: false);

        var result = await new AuroraClaimsTransformation(userManager).TransformAsync(PrincipalFor(user.Id));

        Assert.Null(result.FindFirstValue(AuroraClaims.OrganizationId));
    }

    [Fact]
    public async Task DeletedUser_GetsNoTenantClaim()
    {
        await using var provider = await PrepareDatabaseAsync();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await new AuroraClaimsTransformation(userManager).TransformAsync(PrincipalFor(Guid.NewGuid()));

        Assert.Null(result.FindFirstValue(AuroraClaims.OrganizationId));
    }

    [Fact]
    public async Task ExistingTenantClaim_IsNotReplaced()
    {
        await using var provider = await PrepareDatabaseAsync();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var organization = await AddOrganizationAsync(db);
        var user = await CreateUserAsync(userManager, organization.Id, isActive: true);
        var alreadyResolved = Guid.NewGuid().ToString();

        var principal = PrincipalFor(user.Id);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AuroraClaims.OrganizationId, alreadyResolved));

        var result = await new AuroraClaimsTransformation(userManager).TransformAsync(principal);

        Assert.Equal(alreadyResolved, result.FindFirstValue(AuroraClaims.OrganizationId));
    }

    [Fact]
    public async Task UnauthenticatedPrincipal_IsReturnedUnchanged()
    {
        await using var provider = await PrepareDatabaseAsync();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var result = await new AuroraClaimsTransformation(userManager).TransformAsync(anonymous);

        Assert.Null(result.FindFirstValue(AuroraClaims.OrganizationId));
    }

    private static async Task<ServiceProvider> PrepareDatabaseAsync()
    {
        var connectionString = TestDatabase.ReadDisposableDatabaseConnection();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new TestTenant(null));
        services.AddDbContext<AuroraDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AuroraDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        return provider;
    }

    private static async Task<Organization> AddOrganizationAsync(AuroraDbContext db)
    {
        var organization = new Organization("Claims Test Org", "Testing");
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        return organization;
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager, Guid organizationId, bool isActive)
    {
        var user = new ApplicationUser
        {
            UserName = $"claims-{Guid.NewGuid():N}@example.com",
            Email = $"claims-{Guid.NewGuid():N}@example.com",
            FullName = "Claims Test User",
            OrganizationId = organizationId,
            IsActive = isActive,
        };

        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        return user;
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            authenticationType: "TestCookie"));

    private sealed class TestTenant(Guid? organizationId) : ITenantContext
    {
        public Guid? OrganizationId => organizationId;
    }
}

using Aurora.Modules.Alerts.Domain;
using Aurora.Web.Data;
using Aurora.Web.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Aurora.Tests.Persistence;

/// <summary>
/// Proves that domain events commit in the same transaction as the change that raised them, and that the outbox
/// stays empty when nothing changed. Uses the disposable database described in <see cref="TestDatabase"/>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class OutboxIntegrationTests
{
    private static readonly DateTime RaisedAt = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StagedEvents_CommitWithTheChangeAndClearTheAggregate()
    {
        var options = CreateOptions();
        var organizationId = Guid.NewGuid();

        await using (var setup = new AuroraDbContext(options, new FixedTenant(null)))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        Guid alertId;
        await using (var raise = new AuroraDbContext(options, new FixedTenant(organizationId)))
        {
            var alert = new Alert(organizationId, "monitoring:metric:outbox-test", "Yield below minimum", null, AlertSeverity.Warning, RaisedAt);
            alertId = alert.Id;
            raise.Alerts.Add(alert);

            await raise.SaveChangesAsync();

            Assert.Empty(alert.DomainEvents);
        }

        await using (var resolve = new AuroraDbContext(options, new FixedTenant(organizationId)))
        {
            var alert = await resolve.Alerts.SingleAsync(a => a.Id == alertId);
            alert.Resolve(RaisedAt.AddHours(1));

            await resolve.SaveChangesAsync();
        }

        await using var verify = new AuroraDbContext(options, new FixedTenant(null));
        var messages = await verify.OutboxMessages
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.OccurredOnUtc)
            .ToListAsync();

        Assert.Equal(
            new[] { nameof(AlertRaised), nameof(AlertResolved) },
            messages.Select(m => m.EventType).ToArray());
        Assert.All(messages, m => Assert.Null(m.ProcessedOnUtc));
        Assert.All(messages, m => Assert.Equal(0, m.Attempts));
    }

    [Fact]
    public async Task UnchangedSave_StagesNoEvents()
    {
        var options = CreateOptions();
        var organizationId = Guid.NewGuid();

        await using (var setup = new AuroraDbContext(options, new FixedTenant(null)))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        await using (var save = new AuroraDbContext(options, new FixedTenant(organizationId)))
        {
            await save.SaveChangesAsync();
        }

        await using var verify = new AuroraDbContext(options, new FixedTenant(null));
        Assert.Equal(0, await verify.OutboxMessages.CountAsync());
    }

    private static DbContextOptions<AuroraDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AuroraDbContext>().UseNpgsql(TestDatabase.ReadDisposableDatabaseConnection()).Options;

    private sealed class FixedTenant(Guid? organizationId) : ITenantContext
    {
        public Guid? OrganizationId => organizationId;
    }
}

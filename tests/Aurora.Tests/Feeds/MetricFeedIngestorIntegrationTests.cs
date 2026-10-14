using System.Globalization;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Shared.Kernel;
using Aurora.Tests.Persistence;
using Aurora.Web.Data;
using Aurora.Web.Integration.Feeds;
using Aurora.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aurora.Tests.Feeds;

/// <summary>
/// Runs the metric feed ingestor against the disposable PostgreSQL database. A job whose lease expires is claimed
/// again and runs the same payload a second time, so these checks prove that a re-run records no duplicate reading.
/// Uses the database described in <see cref="TestDatabase"/>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class MetricFeedIngestorIntegrationTests
{
    private const string SourceKey = "fixture-metrics";
    private const string MetricName = "Line 1 cycle time";
    private const string MetricUnit = "seconds";
    private const decimal MaximumThreshold = 30m;
    private const decimal FirstValue = 14.2m;
    private const decimal ChangedValue = 15m;
    private const string ObservedOn = "2026-10-10";
    private static readonly DateTime FixedNow = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RerunningTheSamePayload_RecordsTheReadingOnlyOnce()
    {
        var options = CreateOptions();
        var organizationId = await PrepareDatabaseAsync(options);

        var first = await IngestAsync(options, organizationId, FeedPayload(FirstValue));
        var second = await IngestAsync(options, organizationId, FeedPayload(FirstValue));

        Assert.Equal(1, first.Accepted);
        Assert.Equal(0, first.AlreadyRecorded);
        Assert.Equal(0, second.Accepted);
        Assert.Equal(1, second.AlreadyRecorded);
        Assert.Equal(1, await CountReadingsAsync(options, organizationId));
    }

    [Fact]
    public async Task SameDayWithADifferentValue_IsRecordedAsANewReading()
    {
        var options = CreateOptions();
        var organizationId = await PrepareDatabaseAsync(options);

        await IngestAsync(options, organizationId, FeedPayload(FirstValue));
        var corrected = await IngestAsync(options, organizationId, FeedPayload(ChangedValue));

        Assert.Equal(1, corrected.Accepted);
        Assert.Equal(0, corrected.AlreadyRecorded);
        Assert.Equal(2, await CountReadingsAsync(options, organizationId));
    }

    private static string FeedPayload(decimal value) =>
        "{\"schemaVersion\":1,\"readings\":[{\"metric\":\"" + MetricName + "\",\"observedOn\":\"" + ObservedOn +
        "\",\"value\":" + value.ToString(CultureInfo.InvariantCulture) + "}]}";

    private static async Task<Guid> PrepareDatabaseAsync(DbContextOptions<AuroraDbContext> options)
    {
        var organizationId = Guid.NewGuid();

        await using (var setup = new AuroraDbContext(options, new FixedTenant(null)))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        await using var seed = new AuroraDbContext(options, new FixedTenant(organizationId));
        seed.MetricDefinitions.Add(new MetricDefinition(organizationId, MetricName, MetricUnit, null, MaximumThreshold));
        await seed.SaveChangesAsync();
        return organizationId;
    }

    /// <summary>Each run uses its own context, as a separate job execution does.</summary>
    private static async Task<FeedIngestionSummary> IngestAsync(
        DbContextOptions<AuroraDbContext> options, Guid organizationId, string payload)
    {
        await using var db = new AuroraDbContext(options, new FixedTenant(organizationId));
        var ingestor = new MetricFeedIngestor(
            db,
            new MetricReadingRecorder(db),
            new AuditRecorder(new AuditRepository(db)),
            new FixedClock(FixedNow));

        return await ingestor.IngestAsync(organizationId, SourceKey, payload, CancellationToken.None);
    }

    private static async Task<int> CountReadingsAsync(DbContextOptions<AuroraDbContext> options, Guid organizationId)
    {
        await using var verify = new AuroraDbContext(options, new FixedTenant(null));
        return await verify.MetricReadings.IgnoreQueryFilters().CountAsync(r => r.OrganizationId == organizationId);
    }

    private static DbContextOptions<AuroraDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AuroraDbContext>().UseNpgsql(TestDatabase.ReadDisposableDatabaseConnection()).Options;

    private sealed class FixedTenant(Guid? organizationId) : ITenantContext
    {
        public Guid? OrganizationId => organizationId;
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}

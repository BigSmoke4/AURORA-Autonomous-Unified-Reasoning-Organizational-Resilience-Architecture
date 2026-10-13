using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Decisions.Application;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Reporting.Domain;
using Aurora.Shared.Kernel;
using Aurora.Web.Data;
using Aurora.Web.Data.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Aurora.Tests.Persistence;

/// <summary>
/// Runs the real migration chain against PostgreSQL. CI provides the database through
/// AURORA_TEST_CONNECTION. The database is dropped and recreated, so its name must contain "test".
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.Name)]
public sealed class MigrationIntegrationTests
{
    private const string Phase2MigrationId = "20261013120000_Phase2AuditAnalysisDecisionsAi";
    private const string PreviousMigrationId = "20260901062452_InitialCreate";
    private const string Phase3MigrationId = "20261013140000_Phase3OperationalModules";
    private const string Phase4MigrationId = "20261013160000_Phase4DomainEvents";
    private const string ReportHashA = "12b3b4f77501b884b53acbbe17b0d2d878241a2b0404569105423de1fd319771";

    [Fact]
    public async Task Phase2Migration_AppliesAndRevertsCleanly()
    {
        var options = CreateOptions();

        await using var db = new AuroraDbContext(options, new FixedTenant(null));
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(Phase2MigrationId, await db.Database.GetAppliedMigrationsAsync());

        var migrator = ((IInfrastructure<IServiceProvider>)db.Database).Instance.GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigrationId);
        Assert.DoesNotContain(Phase2MigrationId, await db.Database.GetAppliedMigrationsAsync());

        await db.Database.MigrateAsync();
        Assert.Contains(Phase2MigrationId, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Phase3Migration_AppliesAndRevertsCleanly()
    {
        var options = CreateOptions();

        await using var db = new AuroraDbContext(options, new FixedTenant(null));
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(Phase3MigrationId, await db.Database.GetAppliedMigrationsAsync());

        var migrator = ((IInfrastructure<IServiceProvider>)db.Database).Instance.GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Phase2MigrationId);
        Assert.DoesNotContain(Phase3MigrationId, await db.Database.GetAppliedMigrationsAsync());

        await db.Database.MigrateAsync();
        Assert.Contains(Phase3MigrationId, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Phase4Migration_AppliesAndRevertsCleanly()
    {
        var options = CreateOptions();

        await using var db = new AuroraDbContext(options, new FixedTenant(null));
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(Phase4MigrationId, await db.Database.GetAppliedMigrationsAsync());

        var migrator = ((IInfrastructure<IServiceProvider>)db.Database).Instance.GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Phase3MigrationId);
        Assert.DoesNotContain(Phase4MigrationId, await db.Database.GetAppliedMigrationsAsync());

        await db.Database.MigrateAsync();
        Assert.Contains(Phase4MigrationId, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task TenantFilters_IsolateRowsAcrossOrganizations()
    {
        var options = CreateOptions();
        var userId = Guid.NewGuid();
        var weights = new DecisionScoringWeights();
        var evidence = new CounterfactualEvidence(
            Guid.NewGuid(), "target-edge-attenuation-v1", "Sole supplier", 1_000_000m, 250_000m, 40m);
        var judgements = new ProposalJudgements(80m, 60m, 90, 50m);

        Guid orgAId;
        Guid decisionId;
        await using (var setup = new AuroraDbContext(options, new FixedTenant(null)))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();

            var orgA = new Organization("Integration Org A", "Manufacturing");
            var orgB = new Organization("Integration Org B", "Logistics");
            setup.Organizations.AddRange(orgA, orgB);
            await setup.SaveChangesAsync();
            orgAId = orgA.Id;

            var decision = DecisionProposalFactory.FromCounterfactual(orgA.Id, userId, evidence, judgements, weights);
            decisionId = decision.Id;
            setup.Decisions.Add(decision);
            setup.AnalysisRecords.Add(new AnalysisRecord(orgA.Id, AnalysisKind.Counterfactual, "Analysis A",
                "target-edge-attenuation-v1", "{}", "{}", userId));
            setup.AuditEntries.Add(new AuditEntry(orgA.Id, userId, "analyst", AuditActions.DecisionProposed,
                "Decision", decision.Id.ToString(), AuditOutcome.Success, null, null));
            setup.ExecutiveReportRuns.Add(new ExecutiveReportRun(orgA.Id, new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                userId, "Analyst A", 3, ReportHashA));
            setup.GraphSnapshotRecords.Add(new GraphSnapshotRecord(orgA.Id, new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc),
                2, 1, "{}"));
            setup.BackgroundJobs.Add(new BackgroundJob(orgA.Id, "metric-feed", "{}", new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc)));
            // A platform-level sign-in event has no organization and must stay invisible to every tenant.
            setup.AuditEntries.Add(new AuditEntry(null, null, "unknown@example.com", AuditActions.SignInFailed,
                "User", null, AuditOutcome.Failure, null, null));
            await setup.SaveChangesAsync();
        }

        await using (var tenantA = new AuroraDbContext(options, new FixedTenant(orgAId)))
        {
            var decision = await tenantA.Decisions.SingleAsync();
            Assert.Equal(decisionId, decision.Id);
            Assert.Equal(250_000m, decision.PredictedRevenueImpact);
            Assert.Equal(DecisionSourceKind.Counterfactual, decision.SourceKind);
            Assert.Equal(1, await tenantA.AuditEntries.CountAsync());
            Assert.Equal(1, await tenantA.AnalysisRecords.CountAsync());
            Assert.Equal(1, await tenantA.ExecutiveReportRuns.CountAsync());
            Assert.Equal(1, await tenantA.GraphSnapshotRecords.CountAsync());
            Assert.Equal(1, await tenantA.BackgroundJobs.CountAsync());
        }

        await using (var tenantB = new AuroraDbContext(options, new FixedTenant(Guid.NewGuid())))
        {
            Assert.Equal(0, await tenantB.Decisions.CountAsync());
            Assert.Equal(0, await tenantB.AuditEntries.CountAsync());
            Assert.Equal(0, await tenantB.AnalysisRecords.CountAsync());
            Assert.Equal(0, await tenantB.ExecutiveReportRuns.CountAsync());
            Assert.Equal(0, await tenantB.GraphSnapshotRecords.CountAsync());
            Assert.Equal(0, await tenantB.BackgroundJobs.CountAsync());
        }

        await using (var anonymous = new AuroraDbContext(options, new FixedTenant(null)))
        {
            Assert.Equal(0, await anonymous.Decisions.CountAsync());
            Assert.Equal(0, await anonymous.AuditEntries.CountAsync());
            // The unfiltered count proves that rows exist, so the zero counts above are meaningful.
            Assert.Equal(1, await anonymous.Decisions.IgnoreQueryFilters().CountAsync());
            Assert.Equal(2, await anonymous.AuditEntries.IgnoreQueryFilters().CountAsync());
            Assert.Equal(0, await anonymous.ExecutiveReportRuns.CountAsync());
            Assert.Equal(0, await anonymous.GraphSnapshotRecords.CountAsync());
            Assert.Equal(0, await anonymous.BackgroundJobs.CountAsync());
            Assert.Equal(1, await anonymous.ExecutiveReportRuns.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await anonymous.GraphSnapshotRecords.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await anonymous.BackgroundJobs.IgnoreQueryFilters().CountAsync());
        }
    }

    private static DbContextOptions<AuroraDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AuroraDbContext>().UseNpgsql(ReadDisposableDatabaseConnection()).Options;

    private static string ReadDisposableDatabaseConnection() => TestDatabase.ReadDisposableDatabaseConnection();

    private sealed class FixedTenant(Guid? organizationId) : ITenantContext
    {
        public Guid? OrganizationId => organizationId;
    }
}

using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Contracts;
using Aurora.Modules.Audit.Domain;
using Xunit;

namespace Aurora.Tests;

public class AuditRecorderTests
{
    [Fact]
    public void Record_StagesAnEntryWithItsOutcomeAndActor()
    {
        var repository = new CapturingAuditRepository();
        var recorder = new AuditRecorder(repository);
        var org = Guid.NewGuid();

        recorder.Record(org, Guid.NewGuid(), "analyst@example.test", AuditActions.DecisionApproved,
            "Decision", "decision-1", AuditOutcome.Denied);

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(org, entry.OrganizationId);
        Assert.Equal("analyst@example.test", entry.ActorName);
        Assert.Equal(AuditActions.DecisionApproved, entry.Action);
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
    }

    [Fact]
    public void Record_TruncatesDetailsToTheStoredLimit()
    {
        var repository = new CapturingAuditRepository();
        var recorder = new AuditRecorder(repository);

        recorder.Record(Guid.NewGuid(), null, "system", AuditActions.SimulationFailed, "SimulationRun", null,
            AuditOutcome.Failure, new string('d', AuditRecorder.MaxDetailsLength + 100));

        Assert.Equal(AuditRecorder.MaxDetailsLength, repository.Entries.Single().Details!.Length);
    }

    private sealed class CapturingAuditRepository : IAuditRepository
    {
        public List<AuditEntry> Entries { get; } = new();

        public void Add(AuditEntry entry) => Entries.Add(entry);

        public Task<IReadOnlyList<AuditEntry>> GetRecentAsync(Guid organizationId, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>(Entries.Take(take).ToList());

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}

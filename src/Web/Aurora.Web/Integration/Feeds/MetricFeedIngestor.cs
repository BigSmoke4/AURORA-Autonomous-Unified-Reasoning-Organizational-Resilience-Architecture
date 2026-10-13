using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Web.Data;
using Aurora.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Integration.Feeds;

public sealed record FeedIngestionSummary(string SourceKey, int Accepted, int AlreadyRecorded, int Rejected, int AlertsRaised,
    int AlertsResolved, IReadOnlyList<string> RejectionReasons)
{
    public const int MaxReasonsInSummary = 3;

    public string Describe()
    {
        var text = $"Source {SourceKey}: {Accepted} readings recorded, {AlreadyRecorded} already recorded, {Rejected} rejected, " +
            $"{AlertsRaised} alerts raised, {AlertsResolved} alerts resolved.";
        return RejectionReasons.Count == 0 ? text : $"{text} {string.Join(" ", RejectionReasons.Take(MaxReasonsInSummary))}";
    }
}

/// <summary>
/// Validates a feed payload and records each accepted row through the same recorder as manual entry, so feeds and
/// people judge readings and raise alerts identically. A row whose metric name matches no definition, or matches
/// more than one, is rejected instead of guessed. Each row is committed on its own, so each threshold decision
/// sees the alerts the previous row raised.
/// A reading that matches an existing one for the same metric, day, and value is counted as already recorded and
/// is not stored again. A job that runs twice, after a lease expires, therefore records each observation once.
/// </summary>
public sealed class MetricFeedIngestor(AuroraDbContext db, MetricReadingRecorder recorder, AuditRecorder audit, TimeProvider clock)
{
    /// <summary>Matches the scale of the stored reading column, so a value compares equal to what was saved.</summary>
    private const int StoredValueDecimals = 4;

    public async Task<FeedIngestionSummary> IngestAsync(Guid organizationId, string sourceKey, string payloadJson, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var parsed = MetricFeedParser.Parse(payloadJson, today);

        var definitions = await db.MetricDefinitions.AsNoTracking().ToListAsync(ct);
        var byName = definitions.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var rejected = new List<string>(parsed.Rejected);
        var accepted = 0;
        var duplicates = 0;
        var raised = 0;
        var resolved = 0;
        foreach (var row in parsed.Accepted)
        {
            if (!byName.TryGetValue(row.Metric, out var matches))
            {
                rejected.Add($"Row {row.Index}: metric '{row.Metric}' is not defined.");
                continue;
            }

            if (matches.Count > 1)
            {
                rejected.Add($"Row {row.Index}: metric '{row.Metric}' matches more than one definition.");
                continue;
            }

            var storedValue = decimal.Round(row.Value, StoredValueDecimals, MidpointRounding.AwayFromZero);
            var alreadyRecorded = await db.MetricReadings.AnyAsync(r =>
                r.MetricDefinitionId == matches[0].Id && r.RecordedAtUtc == row.ObservedOnUtc && r.Value == storedValue, ct);
            if (alreadyRecorded)
            {
                duplicates++;
                continue;
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var result = await recorder.RecordAsync(organizationId, matches[0], storedValue, row.ObservedOnUtc, now, ct);
            if (result.RaisedAlertId is not null) raised++;
            if (result.ResolvedAlertId is not null) resolved++;
            accepted++;
            await db.SaveChangesAsync(ct);
        }

        audit.Record(organizationId, null, $"Feed {sourceKey}", AuditActions.MetricFeedIngested, nameof(MetricReading), sourceKey,
            AuditOutcome.Success, $"{accepted} recorded, {rejected.Count} rejected.");

        await db.SaveChangesAsync(ct);
        return new FeedIngestionSummary(sourceKey, accepted, duplicates, rejected.Count, raised, resolved, rejected);
    }
}

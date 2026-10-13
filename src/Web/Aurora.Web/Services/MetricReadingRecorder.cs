using Aurora.Modules.Alerts.Application;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models.Modules;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Services;

/// <summary>What one recorded reading changed. The caller writes the audit trail and commits.</summary>
public sealed record MetricRecordingResult(Guid ReadingId, ThresholdState State, Guid? RaisedAlertId, Guid? ResolvedAlertId);

/// <summary>
/// Records a reading and keeps the metric's single open alert in step with its threshold state (spec section 27).
/// Used by manual entry and by feed ingestion, so both paths judge readings identically.
/// Stages changes only; the caller commits with SaveChangesAsync.
/// </summary>
public sealed class MetricReadingRecorder(AuroraDbContext db)
{
    public async Task<MetricRecordingResult> RecordAsync(
        Guid organizationId,
        MetricDefinition definition,
        decimal value,
        DateTime recordedOn,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var state = MetricThresholdEvaluator.Evaluate(value, definition.MinimumThreshold, definition.MaximumThreshold);
        var reading = new MetricReading(organizationId, definition.Id, value, recordedOn, (int)state);
        db.MetricReadings.Add(reading);

        var sourceKey = AlertSourceKeys.ForMetric(definition.Id);
        var openAlert = await db.Alerts.FirstOrDefaultAsync(a => a.SourceKey == sourceKey && a.Status != AlertStatus.Resolved, ct);

        Guid? raised = null;
        Guid? resolved = null;
        if (state == ThresholdState.Normal)
        {
            if (openAlert is not null)
            {
                openAlert.Resolve(nowUtc);
                resolved = openAlert.Id;
            }
        }
        else if (openAlert is null)
        {
            var alert = new Alert(organizationId, sourceKey, BuildTitle(definition, state),
                BuildDetail(definition, value, recordedOn), AlertSeverity.Warning, nowUtc);
            db.Alerts.Add(alert);
            raised = alert.Id;
        }

        return new MetricRecordingResult(reading.Id, state, raised, resolved);
    }

    private static string BuildTitle(MetricDefinition definition, ThresholdState state) => state == ThresholdState.BelowMinimum
        ? $"{definition.Name} is below its minimum of {ModuleFormatting.Number(definition.MinimumThreshold.GetValueOrDefault())} {definition.Unit}"
        : $"{definition.Name} is above its maximum of {ModuleFormatting.Number(definition.MaximumThreshold.GetValueOrDefault())} {definition.Unit}";

    private static string BuildDetail(MetricDefinition definition, decimal value, DateTime recordedOn) =>
        $"Reading {ModuleFormatting.Number(value)} {definition.Unit} recorded on {ModuleFormatting.Date(recordedOn)}.";
}

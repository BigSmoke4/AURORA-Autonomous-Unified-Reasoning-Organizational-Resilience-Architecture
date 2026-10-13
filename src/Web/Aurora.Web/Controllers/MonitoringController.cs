using Aurora.Modules.Alerts.Application;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>
/// Metric definitions and readings judged against their thresholds (spec section 27). A reading outside its
/// thresholds raises one warning alert for that metric. The alert resolves when the metric returns to range.
/// </summary>
[Authorize(Policy = AuroraPermissions.MonitoringRead)]
public class MonitoringController : ModuleControllerBase
{
    private const string NoReadingsStatus = "No readings";

    public MonitoringController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new MetricDefinitionForm(), new MetricReadingForm { RecordedOn = TodayUtc() }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.MonitoringManage)]
    public async Task<IActionResult> CreateMetric(MetricDefinitionForm form, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, new MetricReadingForm { RecordedOn = TodayUtc() }, ct));

        var definition = new MetricDefinition(org.Id, form.Name, form.Unit, form.MinimumThreshold, form.MaximumThreshold);
        Db.MetricDefinitions.Add(definition);
        RecordAudit(org, AuditActions.MetricDefinitionCreated, nameof(MetricDefinition), definition.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Metric '{definition.Name}' added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.MonitoringManage)]
    public async Task<IActionResult> RecordReading(MetricReadingForm form, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var metricId = form.MetricDefinitionId.GetValueOrDefault();
        var definition = metricId == Guid.Empty
            ? null
            : await Db.MetricDefinitions.FirstOrDefaultAsync(d => d.Id == metricId, ct);
        if (definition is null) ModelState.AddModelError(nameof(form.MetricDefinitionId), "The selected metric was not found.");
        if (!ModelState.IsValid || definition is null)
            return View(nameof(Index), await BuildIndexAsync(new MetricDefinitionForm(), form, ct));

        var now = TodayUtc();
        var state = MetricThresholdEvaluator.Evaluate(form.Value, definition.MinimumThreshold, definition.MaximumThreshold);
        var reading = new MetricReading(org.Id, definition.Id, form.Value, form.RecordedOn, (int)state);
        Db.MetricReadings.Add(reading);
        RecordAudit(org, AuditActions.MetricReadingRecorded, nameof(MetricReading), reading.Id.ToString(), state.ToString());

        var sourceKey = AlertSourceKeys.ForMetric(definition.Id);
        var openAlert = await Db.Alerts.FirstOrDefaultAsync(a => a.SourceKey == sourceKey && a.Status != AlertStatus.Resolved, ct);

        if (state == ThresholdState.Normal)
        {
            if (openAlert is not null)
            {
                openAlert.Resolve(now);
                RecordAudit(org, AuditActions.AlertResolved, nameof(Alert), openAlert.Id.ToString());
            }
        }
        else if (openAlert is null)
        {
            var alert = new Alert(org.Id, sourceKey, BuildTitle(definition, state), BuildDetail(definition, form), AlertSeverity.Warning, now);
            Db.Alerts.Add(alert);
            RecordAudit(org, AuditActions.AlertRaised, nameof(Alert), alert.Id.ToString(), state.ToString());
        }

        await Db.SaveChangesAsync(ct);
        Success(state == ThresholdState.Normal ? "Reading saved, within thresholds." : "Reading saved, outside thresholds. An alert was raised.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<MonitoringIndexViewModel> BuildIndexAsync(MetricDefinitionForm definitionForm, MetricReadingForm readingForm, CancellationToken ct)
    {
        var definitions = await Db.MetricDefinitions.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var rows = new List<MetricRow>(definitions.Count);

        foreach (var definition in definitions)
        {
            var latest = await Db.MetricReadings.AsNoTracking()
                .Where(r => r.MetricDefinitionId == definition.Id)
                .OrderByDescending(r => r.RecordedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (latest is null)
            {
                rows.Add(new MetricRow(definition.Id, definition.Name, definition.Unit, definition.MinimumThreshold,
                    definition.MaximumThreshold, null, null, NoReadingsStatus, ModuleFormatting.ToneWarning));
                continue;
            }

            var state = (ThresholdState)latest.ThresholdState;
            rows.Add(new MetricRow(definition.Id, definition.Name, definition.Unit, definition.MinimumThreshold,
                definition.MaximumThreshold, latest.Value, latest.RecordedAtUtc, ModuleFormatting.Humanize(state.ToString()),
                state == ThresholdState.Normal ? ModuleFormatting.ToneHealthy : ModuleFormatting.ToneCritical));
        }

        var metricOptions = definitions
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), readingForm.MetricDefinitionId == d.Id))
            .ToList();

        return new MonitoringIndexViewModel(rows, metricOptions, definitionForm, readingForm);
    }

    private static string BuildTitle(MetricDefinition definition, ThresholdState state) => state == ThresholdState.BelowMinimum
        ? $"{definition.Name} is below its minimum of {ModuleFormatting.Number(definition.MinimumThreshold.GetValueOrDefault())} {definition.Unit}"
        : $"{definition.Name} is above its maximum of {ModuleFormatting.Number(definition.MaximumThreshold.GetValueOrDefault())} {definition.Unit}";

    private static string BuildDetail(MetricDefinition definition, MetricReadingForm form) =>
        $"Reading {ModuleFormatting.Number(form.Value)} {definition.Unit} recorded on {ModuleFormatting.Date(form.RecordedOn)}.";
}

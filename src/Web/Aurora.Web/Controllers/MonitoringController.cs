using Aurora.Modules.Alerts.Application;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Web.Data;
using Aurora.Web.Data.Jobs;
using Aurora.Web.Data.Repositories;
using Aurora.Web.Infrastructure.Jobs;
using Aurora.Web.Integration.Feeds;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Services;
using Aurora.Web.Validation;
using FluentValidation;
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
    private const int RecentJobCount = 20;

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
    public async Task<IActionResult> CreateMetric([Bind(Prefix = nameof(MonitoringIndexViewModel.DefinitionForm))] MetricDefinitionForm form, [FromServices] IValidator<MetricDefinitionForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(MonitoringIndexViewModel.DefinitionForm));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, new MetricReadingForm { RecordedOn = TodayUtc() }, ct));

        var definition = new MetricDefinition(org.Id, form.Name, form.Unit, form.MinimumThreshold, form.MaximumThreshold);
        Entities<MetricDefinition>().Add(definition);
        RecordAudit(org, AuditActions.MetricDefinitionCreated, nameof(MetricDefinition), definition.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Metric '{definition.Name}' added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.MonitoringManage)]
    public async Task<IActionResult> RecordReading([Bind(Prefix = nameof(MonitoringIndexViewModel.ReadingForm))] MetricReadingForm form, [FromServices] IValidator<MetricReadingForm> validator,
        [FromServices] MetricReadingRecorder recorder, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(MonitoringIndexViewModel.ReadingForm));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var metricId = form.MetricDefinitionId.GetValueOrDefault();
        var definition = metricId == Guid.Empty ? null : await Entities<MetricDefinition>().FindAsync(metricId, ct);
        if (definition is null) ModelState.AddModelError($"{nameof(MonitoringIndexViewModel.ReadingForm)}.{nameof(MetricReadingForm.MetricDefinitionId)}", "The selected metric was not found.");
        if (!ModelState.IsValid || definition is null)
            return View(nameof(Index), await BuildIndexAsync(new MetricDefinitionForm(), form, ct));

        var result = await recorder.RecordAsync(org.Id, definition, form.Value, form.RecordedOn, TodayUtc(), ct);
        RecordAudit(org, AuditActions.MetricReadingRecorded, nameof(MetricReading), result.ReadingId.ToString(), result.State.ToString());
        if (result.ResolvedAlertId is { } resolvedId) RecordAudit(org, AuditActions.AlertResolved, nameof(Alert), resolvedId.ToString());
        if (result.RaisedAlertId is { } raisedId) RecordAudit(org, AuditActions.AlertRaised, nameof(Alert), raisedId.ToString(), result.State.ToString());

        await Db.SaveChangesAsync(ct);
        Success(result.State == ThresholdState.Normal ? "Reading saved, within thresholds." : "Reading saved, outside thresholds. An alert was raised.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.MonitoringManage)]
    public async Task<IActionResult> QueueFeedIngestion([FromServices] BackgroundJobQueue queue, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        queue.Enqueue(org.Id, MetricFeedJobHandler.JobKind, MetricFeedJobHandler.BuildPayload(FixtureMetricFeedAdapter.Key));
        await Db.SaveChangesAsync(ct);
        Success("Feed ingestion queued. Its result appears under Background jobs when it finishes.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<MonitoringIndexViewModel> BuildIndexAsync(MetricDefinitionForm definitionForm, MetricReadingForm readingForm, CancellationToken ct)
    {
        var definitions = await Entities<MetricDefinition>().ListAsync(null, q => q.OrderBy(d => d.Name), ct);
        var rows = new List<MetricRow>(definitions.Count);

        foreach (var definition in definitions)
        {
            var latest = await Entities<MetricReading>().FirstOrDefaultAsync(
                r => r.MetricDefinitionId == definition.Id, q => q.OrderByDescending(r => r.RecordedAtUtc), ct);

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

        var jobs = await Entities<BackgroundJob>().ListAsync(null, q => q.OrderByDescending(j => j.CreatedAtUtc), ct, RecentJobCount);
        var jobRows = jobs.Select(j => new BackgroundJobRow(j.Id, j.Kind, ModuleFormatting.Humanize(j.Status.ToString()),
            j.Attempts, j.ResultSummary, j.LastError, j.CompletedAtUtc)).ToList();

        return new MonitoringIndexViewModel(rows, metricOptions, definitionForm, readingForm, jobRows);
    }
}

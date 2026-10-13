using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Operational alert triage: acknowledge and resolve, with every transition audited (spec section 28).</summary>
[Authorize(Policy = AuroraPermissions.AlertsRead)]
public class AlertsController : ModuleControllerBase
{
    private const int AlertRowLimit = 200;

    public AlertsController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        var alerts = await Db.Alerts.AsNoTracking()
            .OrderBy(a => a.Status == AlertStatus.Resolved)
            .ThenByDescending(a => a.Severity)
            .ThenByDescending(a => a.RaisedAtUtc)
            .Take(AlertRowLimit)
            .ToListAsync(ct);

        var canManage = User.HasClaim(AuroraClaims.Permission, AuroraPermissions.AlertsAcknowledge);
        var rows = alerts.Select(a => new AlertRow(
            a.Id,
            a.Title,
            a.Detail,
            ModuleFormatting.Humanize(a.Severity.ToString()),
            ModuleFormatting.Humanize(a.Status.ToString()),
            ToneFor(a),
            a.RaisedAtUtc,
            a.AcknowledgedAtUtc,
            canManage && a.Status == AlertStatus.Open,
            canManage && a.Status != AlertStatus.Resolved)).ToList();

        return View(new AlertsIndexViewModel(rows, canManage));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.AlertsAcknowledge)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (CurrentUserId() is not { } userId) return Forbid();

        var alert = await Db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return NotFound();

        try
        {
            alert.Acknowledge(userId, TodayUtc());
        }
        catch (InvalidOperationException ex)
        {
            Failure(ex.Message);
            return RedirectToAction(nameof(Index));
        }

        RecordAudit(org, AuditActions.AlertAcknowledged, nameof(Alert), alert.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success("Alert acknowledged.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.AlertsAcknowledge)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var alert = await Db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null) return NotFound();

        try
        {
            alert.Resolve(TodayUtc());
        }
        catch (InvalidOperationException ex)
        {
            Failure(ex.Message);
            return RedirectToAction(nameof(Index));
        }

        RecordAudit(org, AuditActions.AlertResolved, nameof(Alert), alert.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success("Alert resolved.");
        return RedirectToAction(nameof(Index));
    }

    private static string ToneFor(Alert alert)
    {
        if (alert.Status == AlertStatus.Resolved) return ModuleFormatting.ToneHealthy;
        return alert.Severity switch
        {
            AlertSeverity.Critical => ModuleFormatting.ToneCritical,
            AlertSeverity.Warning => ModuleFormatting.ToneWarning,
            _ => ModuleFormatting.ToneHealthy,
        };
    }
}

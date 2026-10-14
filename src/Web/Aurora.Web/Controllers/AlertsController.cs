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

        var alerts = await Entities<Alert>().ListAsync(null,
            q => q.OrderBy(a => a.Status == AlertStatus.Resolved).ThenByDescending(a => a.Severity).ThenByDescending(a => a.RaisedAtUtc),
            ct, AlertRowLimit);

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

        var alert = await Entities<Alert>().FirstOrDefaultAsync(a => a.Id == id, null, ct);
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
        return await SaveTransitionAsync("Alert acknowledged.", ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.AlertsAcknowledge)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var alert = await Entities<Alert>().FirstOrDefaultAsync(a => a.Id == id, null, ct);
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
        return await SaveTransitionAsync("Alert resolved.", ct);
    }

    /// <summary>
    /// Saves a transition. When another request changed the alert first, the save conflicts; the user is told to
    /// reload instead of receiving a server error, and the audit row of this request is not written.
    /// </summary>
    private async Task<IActionResult> SaveTransitionAsync(string successMessage, CancellationToken ct)
    {
        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Failure("The alert changed while you were working on it. Reload the page and try again.");
            return RedirectToAction(nameof(Index));
        }

        Success(successMessage);
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

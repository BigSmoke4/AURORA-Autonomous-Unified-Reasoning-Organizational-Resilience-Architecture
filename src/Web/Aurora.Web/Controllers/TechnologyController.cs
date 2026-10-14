using Aurora.Modules.Audit.Application;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Technology.Application;
using Aurora.Modules.Technology.Domain;
using Aurora.Web.Data;
using Aurora.Web.Infrastructure.Jobs;
using Aurora.Web.Integration.Feeds;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>
/// Patch compliance for each technology asset against its service-level window (spec section 22), and the exposure of
/// each patch policy to the vulnerability advisories imported for its software product.
/// </summary>
[Authorize(Policy = AuroraPermissions.TechnologyRead)]
public class TechnologyController : ModuleControllerBase
{
    private const string NoPolicyStatus = "No policy";

    public TechnologyController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new PatchPolicyForm { LastPatchedOn = TodayUtc() }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.TechnologyManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(TechnologyIndexViewModel.Form))] PatchPolicyForm form, [FromServices] IValidator<PatchPolicyForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(TechnologyIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var assetId = form.TechnologyAssetId.GetValueOrDefault();
        if (assetId != Guid.Empty)
        {
            if (!await Entities<TechnologyAsset>().AnyAsync(a => a.Id == assetId, ct))
                ModelState.AddModelError(nameof(form.TechnologyAssetId), "The selected asset was not found.");
            else if (await Entities<PatchPolicy>().AnyAsync(p => p.TechnologyAssetId == assetId, ct))
                ModelState.AddModelError(nameof(form.TechnologyAssetId), "This asset already has a patch policy. Record a patch against it instead.");
        }

        if (form.LastPatchedOn.Date > TodayUtc().Date)
            ModelState.AddModelError(nameof(form.LastPatchedOn), "The last patch date cannot be in the future.");
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var policy = new PatchPolicy(org.Id, assetId, form.LastPatchedOn, form.PatchSlaDays, form.SoftwareProduct);
        Entities<PatchPolicy>().Add(policy);
        RecordAudit(org, AuditActions.TechnologyPatchPolicyCreated, nameof(PatchPolicy), policy.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success("Patch policy saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.TechnologyManage)]
    public async Task<IActionResult> RecordPatch(RecordPatchForm form, [FromServices] IValidator<RecordPatchForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        if (!ModelState.IsValid)
        {
            Failure("The patch could not be recorded. Check the date and try again.");
            return RedirectToAction(nameof(Index));
        }

        if (form.PatchedOn.Date > TodayUtc().Date)
        {
            Failure("The patch date cannot be in the future.");
            return RedirectToAction(nameof(Index));
        }

        var policy = await Entities<PatchPolicy>().FirstOrDefaultAsync(p => p.Id == form.PatchPolicyId.GetValueOrDefault(), null, ct);
        if (policy is null) return NotFound();

        try
        {
            policy.RecordPatch(form.PatchedOn);
        }
        catch (InvalidOperationException ex)
        {
            Failure(ex.Message);
            return RedirectToAction(nameof(Index));
        }

        RecordAudit(org, AuditActions.TechnologyPatchRecorded, nameof(PatchPolicy), policy.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success("Patch recorded.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Queues an import from the vulnerability feed. The import runs in the background like other feed jobs.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.TechnologyManage)]
    public async Task<IActionResult> QueueVulnerabilityImport([FromServices] BackgroundJobQueue queue, [FromServices] FeedSourceCatalog sources, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        queue.Enqueue(org.Id, VulnerabilityFeedJobHandler.JobKind, VulnerabilityFeedJobHandler.BuildPayload(sources.VulnerabilitySourceKey));
        await Db.SaveChangesAsync(ct);

        Success("Vulnerability import queued. Its result appears under Background jobs when it finishes.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<TechnologyIndexViewModel> BuildIndexAsync(PatchPolicyForm form, CancellationToken ct)
    {
        var assets = await Entities<TechnologyAsset>().ListAsync(null, q => q.OrderBy(a => a.Name), ct);
        var policies = await Entities<PatchPolicy>().ListAsync(null, null, ct);
        var advisories = await Entities<VulnerabilityAdvisory>().ListAsync(null, null, ct);
        var policyByAsset = policies.ToDictionary(p => p.TechnologyAssetId);
        var now = TodayUtc();

        var rows = assets.Select(asset =>
        {
            var type = ModuleFormatting.Humanize(asset.AssetType.ToString());
            if (!policyByAsset.TryGetValue(asset.Id, out var policy))
            {
                return new TechnologyRow(asset.Id, null, asset.Name, type, asset.Criticality, null, null, null, null, null,
                    NoPolicyStatus, ModuleFormatting.ToneWarning, null, null, null, null);
            }

            var compliance = PatchComplianceCalculator.Evaluate(policy.LastPatchedOnUtc, policy.PatchSlaDays, now);
            var exposure = PatchExposureCalculator.Evaluate(policy, advisories, now);
            return new TechnologyRow(asset.Id, policy.Id, asset.Name, type, asset.Criticality, policy.LastPatchedOnUtc,
                policy.PatchSlaDays, compliance.DaysSincePatch, compliance.DaysOverSla, compliance.DueOnUtc,
                ModuleFormatting.Humanize(compliance.Status.ToString()),
                compliance.Status == PatchStatus.Overdue ? ModuleFormatting.ToneCritical : ModuleFormatting.ToneHealthy,
                policy.SoftwareProduct, exposure.AdvisoryCount, exposure.OverdueCount, exposure.HighestCvss);
        }).ToList();

        var assetOptions = assets
            .Select(a => new SelectListItem(a.Name, a.Id.ToString(), form.TechnologyAssetId == a.Id))
            .ToList();

        return new TechnologyIndexViewModel(rows, assetOptions, form);
    }
}

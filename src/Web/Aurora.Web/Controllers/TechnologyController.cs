using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Technology.Application;
using Aurora.Modules.Technology.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using FluentValidation;
using Aurora.Web.Validation;
namespace Aurora.Web.Controllers;

/// <summary>Patch compliance for each technology asset against its service-level window (spec section 22).</summary>
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
    public async Task<IActionResult> Create(PatchPolicyForm form, [FromServices] IValidator<PatchPolicyForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var assetId = form.TechnologyAssetId.GetValueOrDefault();
        if (assetId != Guid.Empty)
        {
            if (!await Db.TechnologyAssets.AnyAsync(a => a.Id == assetId, ct))
                ModelState.AddModelError(nameof(form.TechnologyAssetId), "The selected asset was not found.");
            else if (await Db.TechnologyPatchPolicies.AnyAsync(p => p.TechnologyAssetId == assetId, ct))
                ModelState.AddModelError(nameof(form.TechnologyAssetId), "This asset already has a patch policy. Record a patch against it instead.");
        }

        if (form.LastPatchedOn.Date > TodayUtc().Date)
            ModelState.AddModelError(nameof(form.LastPatchedOn), "The last patch date cannot be in the future.");
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var policy = new PatchPolicy(org.Id, assetId, form.LastPatchedOn, form.PatchSlaDays);
        Db.TechnologyPatchPolicies.Add(policy);
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

        var policy = await Db.TechnologyPatchPolicies.FirstOrDefaultAsync(p => p.Id == form.PatchPolicyId.GetValueOrDefault(), ct);
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

    private async Task<TechnologyIndexViewModel> BuildIndexAsync(PatchPolicyForm form, CancellationToken ct)
    {
        var assets = await Db.TechnologyAssets.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);
        var policies = await Db.TechnologyPatchPolicies.AsNoTracking().ToListAsync(ct);
        var policyByAsset = policies.ToDictionary(p => p.TechnologyAssetId);
        var now = TodayUtc();

        var rows = assets.Select(asset =>
        {
            if (!policyByAsset.TryGetValue(asset.Id, out var policy))
            {
                return new TechnologyRow(asset.Id, null, asset.Name, ModuleFormatting.Humanize(asset.AssetType.ToString()),
                    asset.Criticality, null, null, null, null, null, NoPolicyStatus, ModuleFormatting.ToneWarning);
            }

            var compliance = PatchComplianceCalculator.Evaluate(policy.LastPatchedOnUtc, policy.PatchSlaDays, now);
            return new TechnologyRow(asset.Id, policy.Id, asset.Name, ModuleFormatting.Humanize(asset.AssetType.ToString()),
                asset.Criticality, policy.LastPatchedOnUtc, policy.PatchSlaDays, compliance.DaysSincePatch,
                compliance.DaysOverSla, compliance.DueOnUtc,
                ModuleFormatting.Humanize(compliance.Status.ToString()),
                compliance.Status == PatchStatus.Overdue ? ModuleFormatting.ToneCritical : ModuleFormatting.ToneHealthy);
        }).ToList();

        var assetOptions = assets
            .Select(a => new SelectListItem(a.Name, a.Id.ToString(), form.TechnologyAssetId == a.Id))
            .ToList();

        return new TechnologyIndexViewModel(rows, assetOptions, form);
    }
}

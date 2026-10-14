using Aurora.Modules.Assets.Application;
using Aurora.Modules.Assets.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Straight-line depreciation and net book value for physical assets (spec section 24).</summary>
[Authorize(Policy = AuroraPermissions.AssetsRead)]
public class AssetsController : ModuleControllerBase
{
    public AssetsController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new PhysicalAssetForm { InServiceOn = TodayUtc() }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.AssetsManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(AssetsIndexViewModel.Form))] PhysicalAssetForm form, [FromServices] IValidator<PhysicalAssetForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(AssetsIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var asset = new PhysicalAsset(org.Id, form.Name, form.Category, form.AcquisitionCost, form.ResidualValue, form.InServiceOn, form.UsefulLifeYears);
        Entities<PhysicalAsset>().Add(asset);
        RecordAudit(org, AuditActions.AssetRegistered, nameof(PhysicalAsset), asset.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Asset '{asset.Name}' registered.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<AssetsIndexViewModel> BuildIndexAsync(PhysicalAssetForm form, CancellationToken ct)
    {
        var assets = await Entities<PhysicalAsset>().ListAsync(null, q => q.OrderBy(a => a.Name), ct);
        var now = TodayUtc();

        var rows = assets.Select(a =>
        {
            var depreciation = StraightLineDepreciationCalculator.Evaluate(
                a.AcquisitionCost, a.ResidualValue, a.UsefulLifeYears, a.InServiceOnUtc, now);
            return new AssetRow(
                a.Name,
                a.Category,
                a.AcquisitionCost,
                depreciation.AccumulatedDepreciation,
                depreciation.NetBookValue,
                depreciation.AnnualCharge,
                depreciation.RemainingLifeYears,
                depreciation.FullyDepreciated);
        }).ToList();

        return new AssetsIndexViewModel(
            rows,
            assets.Sum(a => a.AcquisitionCost),
            rows.Sum(r => r.NetBookValue),
            form);
    }
}

using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Operations.Application;
using Aurora.Modules.Operations.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Plan attainment and first-pass yield per factory period (spec section 21).</summary>
[Authorize(Policy = AuroraPermissions.OperationsRead)]
public class OperationsController : ModuleControllerBase
{
    private const int RecentRecordLimit = 200;
    private const string UnknownFactory = "Unknown factory";

    public OperationsController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new ProductionRecordForm { PeriodStart = TodayUtc() }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.OperationsManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(OperationsIndexViewModel.Form))] ProductionRecordForm form, [FromServices] IValidator<ProductionRecordForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(OperationsIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var factoryId = form.FactoryId.GetValueOrDefault();
        if (factoryId != Guid.Empty && !await Entities<Factory>().AnyAsync(f => f.Id == factoryId, ct))
            ModelState.AddModelError(nameof(form.FactoryId), "The selected factory was not found.");
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var record = new ProductionRecord(org.Id, factoryId, form.PeriodStart, form.PlannedUnits, form.ActualUnits, form.DefectiveUnits);
        Entities<ProductionRecord>().Add(record);
        RecordAudit(org, AuditActions.OperationsProductionRecorded, nameof(ProductionRecord), record.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success("Production record saved.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<OperationsIndexViewModel> BuildIndexAsync(ProductionRecordForm form, CancellationToken ct)
    {
        var factories = await Entities<Factory>().SelectAsync(f => new { f.Id, f.Name }, null, q => q.OrderBy(f => f.Name), ct);
        var factoryNames = factories.ToDictionary(f => f.Id, f => f.Name);

        var records = await Entities<ProductionRecord>().ListAsync(null, q => q.OrderByDescending(r => r.PeriodStartUtc), ct, RecentRecordLimit);

        var rows = records.Select(r =>
        {
            var performance = ProductionPerformanceCalculator.Evaluate(r.PlannedUnits, r.ActualUnits, r.DefectiveUnits);
            return new ProductionRow(
                factoryNames.GetValueOrDefault(r.FactoryId, UnknownFactory),
                r.PeriodStartUtc,
                r.PlannedUnits,
                r.ActualUnits,
                r.DefectiveUnits,
                performance.AttainmentPct,
                performance.FirstPassYieldPct,
                ModuleFormatting.Humanize(performance.AttainmentStatus.ToString()),
                ModuleFormatting.Humanize(performance.YieldStatus.ToString()),
                ToneFor(performance));
        }).ToList();

        var factoryOptions = factories
            .Select(f => new SelectListItem(f.Name, f.Id.ToString(), form.FactoryId == f.Id))
            .ToList();

        return new OperationsIndexViewModel(rows, factoryOptions, form);
    }

    private static string ToneFor(ProductionPerformanceResult performance)
    {
        var behind = performance.AttainmentStatus == AttainmentStatus.Behind;
        var yieldBelow = performance.YieldStatus is YieldStatus.BelowTarget or YieldStatus.NoOutput;
        if (behind && yieldBelow) return ModuleFormatting.ToneCritical;
        return behind || yieldBelow ? ModuleFormatting.ToneWarning : ModuleFormatting.ToneHealthy;
    }
}

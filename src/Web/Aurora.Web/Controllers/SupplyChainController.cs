using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.SupplyChain.Application;
using Aurora.Modules.SupplyChain.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using FluentValidation;
using Aurora.Web.Validation;
namespace Aurora.Web.Controllers;

/// <summary>Inventory cover and reorder points, plus supplier capacity stretch (spec section 26).</summary>
[Authorize(Policy = AuroraPermissions.SupplyChainRead)]
public class SupplyChainController : ModuleControllerBase
{
    public SupplyChainController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new InventoryPositionForm(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.SupplyChainManage)]
    public async Task<IActionResult> Create(InventoryPositionForm form, [FromServices] IValidator<InventoryPositionForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var position = new InventoryPosition(org.Id, form.Sku, form.Location, form.OnHandUnits, form.AverageDailyDemand,
            form.ReplenishmentLeadTimeDays, form.SafetyStockUnits);
        Db.InventoryPositions.Add(position);
        RecordAudit(org, AuditActions.SupplyPositionRecorded, nameof(InventoryPosition), position.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Inventory position for {position.Sku} saved.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<SupplyChainIndexViewModel> BuildIndexAsync(InventoryPositionForm form, CancellationToken ct)
    {
        var positions = await Db.InventoryPositions.AsNoTracking().OrderBy(p => p.Sku).ThenBy(p => p.Location).ToListAsync(ct);
        var rows = positions.Select(p =>
        {
            var coverage = SupplyCoverageCalculator.Evaluate(p.OnHandUnits, p.AverageDailyDemand, p.ReplenishmentLeadTimeDays, p.SafetyStockUnits);
            return new InventoryRow(
                p.Sku,
                p.Location,
                p.OnHandUnits,
                p.AverageDailyDemand,
                p.ReplenishmentLeadTimeDays,
                p.SafetyStockUnits,
                coverage.ReorderPointUnits,
                coverage.DaysOfCover,
                ModuleFormatting.Humanize(coverage.State.ToString()),
                ToneFor(coverage.State));
        }).ToList();

        var suppliers = await Db.Suppliers.AsNoTracking()
            .Select(s => new StretchedSupplierRow(s.Name, s.Location, s.CapacityUtilizationPct))
            .ToListAsync(ct);
        var stretched = suppliers
            .Where(s => SupplierCapacityRule.IsStretched(s.UtilizationPct))
            .OrderByDescending(s => s.UtilizationPct)
            .ToList();

        return new SupplyChainIndexViewModel(rows, stretched, form);
    }

    private static string ToneFor(InventoryState state) => state switch
    {
        InventoryState.Covered => ModuleFormatting.ToneHealthy,
        InventoryState.BelowReorderPoint => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneCritical,
    };
}

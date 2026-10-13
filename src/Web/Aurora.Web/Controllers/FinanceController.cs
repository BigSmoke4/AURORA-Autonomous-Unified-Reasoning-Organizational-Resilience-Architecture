using Aurora.Modules.Audit.Application;
using Aurora.Modules.Finance.Application;
using Aurora.Modules.Finance.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using FluentValidation;
using Aurora.Web.Validation;
namespace Aurora.Web.Controllers;

/// <summary>Budget variance and utilization per cost center (spec section 20).</summary>
[Authorize(Policy = AuroraPermissions.FinanceRead)]
public class FinanceController : ModuleControllerBase
{
    public FinanceController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new CostCenterForm(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.FinanceManage)]
    public async Task<IActionResult> Create(CostCenterForm form, [FromServices] IValidator<CostCenterForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var code = form.Code.Trim();
        if (await Db.CostCenters.AnyAsync(c => c.Code == code, ct))
            ModelState.AddModelError(nameof(form.Code), "A cost center with this code already exists.");
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var center = new CostCenter(org.Id, code, form.Name, form.AnnualBudget, form.ActualSpend);
        Db.CostCenters.Add(center);
        RecordAudit(org, AuditActions.FinanceCostCenterCreated, nameof(CostCenter), center.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Cost center {center.Code} added.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<FinanceIndexViewModel> BuildIndexAsync(CostCenterForm form, CancellationToken ct)
    {
        var centers = await Db.CostCenters.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        var rows = centers.Select(c =>
        {
            var position = BudgetPositionCalculator.Evaluate(c.AnnualBudget, c.ActualSpend);
            return new CostCenterRow(
                c.Code,
                c.Name,
                c.AnnualBudget,
                c.ActualSpend,
                position.Variance,
                position.UtilizationPct,
                ModuleFormatting.Humanize(position.Status.ToString()),
                ToneFor(position.Status));
        }).ToList();

        return new FinanceIndexViewModel(
            rows,
            centers.Sum(c => c.AnnualBudget),
            centers.Sum(c => c.ActualSpend),
            form);
    }

    private static string ToneFor(BudgetStatus status) => status switch
    {
        BudgetStatus.WithinBudget => ModuleFormatting.ToneHealthy,
        BudgetStatus.NearLimit => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneCritical,
    };
}

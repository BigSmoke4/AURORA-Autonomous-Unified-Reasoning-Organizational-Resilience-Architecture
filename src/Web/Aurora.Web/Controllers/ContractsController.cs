using Aurora.Modules.Audit.Application;
using Aurora.Modules.Contracts.Application;
using Aurora.Modules.Contracts.Domain;
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

/// <summary>Contract term, renewal notice windows and delay penalty exposure (spec section 25).</summary>
[Authorize(Policy = AuroraPermissions.ContractsRead)]
public class ContractsController : ModuleControllerBase
{
    public ContractsController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(int delayDays, CancellationToken ct)
    {
        if (delayDays < 0 || delayDays > ModuleFormatting.MaximumDelayDays) return BadRequest();

        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new SupplierContractForm { StartsOn = TodayUtc(), EndsOn = TodayUtc() }, delayDays, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.ContractsManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(ContractsIndexViewModel.Form))] SupplierContractForm form, [FromServices] IValidator<SupplierContractForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(ContractsIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, 0, ct));

        var contract = new SupplierContract(org.Id, form.Reference, form.Counterparty, form.StartsOn, form.EndsOn,
            form.AnnualValue, form.LatePenaltyPerDay, form.NoticePeriodDays);
        Entities<SupplierContract>().Add(contract);
        RecordAudit(org, AuditActions.ContractRegistered, nameof(SupplierContract), contract.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Contract {contract.Reference} registered.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<ContractsIndexViewModel> BuildIndexAsync(SupplierContractForm form, int delayDays, CancellationToken ct)
    {
        var contracts = await Entities<SupplierContract>().ListAsync(null, q => q.OrderBy(c => c.EndsOnUtc), ct);
        var now = TodayUtc();

        var rows = contracts.Select(c =>
        {
            var status = ContractStatusCalculator.Evaluate(c.StartsOnUtc, c.EndsOnUtc, c.NoticePeriodDays, now);
            return new ContractRow(
                c.Reference,
                c.Counterparty,
                c.StartsOnUtc,
                c.EndsOnUtc,
                status.DaysRemaining,
                ModuleFormatting.Humanize(status.State.ToString()),
                ToneFor(status.State),
                c.AnnualValue,
                ContractStatusCalculator.DelayPenaltyExposure(c.LatePenaltyPerDay, delayDays));
        }).ToList();

        return new ContractsIndexViewModel(rows, delayDays, form);
    }

    private static string ToneFor(ContractState state) => state switch
    {
        ContractState.Expired => ModuleFormatting.ToneCritical,
        ContractState.RenewalNoticeDue => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneHealthy,
    };
}

using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Workforce.Application;
using Aurora.Modules.Workforce.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Critical-role headcount coverage measured against the digital-twin employee roster (spec section 19).</summary>
[Authorize(Policy = AuroraPermissions.WorkforceRead)]
public class WorkforceController : ModuleControllerBase
{
    public WorkforceController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new WorkforceRoleForm(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.WorkforceManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(WorkforceIndexViewModel.Form))] WorkforceRoleForm form, [FromServices] IValidator<WorkforceRoleForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(WorkforceIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var role = new CriticalRole(org.Id, form.Name, form.RoleTitle, form.RequiredHeadcount, form.Criticality);
        Entities<CriticalRole>().Add(role);
        RecordAudit(org, AuditActions.WorkforceRoleCreated, nameof(CriticalRole), role.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Critical role '{role.Name}' added.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<WorkforceIndexViewModel> BuildIndexAsync(WorkforceRoleForm form, CancellationToken ct)
    {
        var roles = await Entities<CriticalRole>().ListAsync(null, q => q.OrderBy(r => r.Name), ct);
        var roleOfEachEmployee = await Entities<Employee>().SelectAsync(e => e.Role, null, null, ct);
        var headcountByRole = roleOfEachEmployee
            .GroupBy(role => role, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var rows = roles.Select(role =>
        {
            var actual = headcountByRole.GetValueOrDefault(role.RoleTitle);
            var coverage = WorkforceCoverageCalculator.Evaluate(role.RequiredHeadcount, actual);
            return new WorkforceRow(
                role.Name,
                role.RoleTitle,
                role.Criticality,
                role.RequiredHeadcount,
                actual,
                coverage.CoveragePct,
                ModuleFormatting.Humanize(coverage.Status.ToString()),
                ToneFor(coverage.Status));
        }).ToList();

        var knownRoleTitles = headcountByRole.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        return new WorkforceIndexViewModel(rows, knownRoleTitles, form);
    }

    private static string ToneFor(CoverageStatus status) => status switch
    {
        CoverageStatus.Covered => ModuleFormatting.ToneHealthy,
        CoverageStatus.SingleThreaded => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneCritical,
    };
}

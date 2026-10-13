using Aurora.Modules.Customers.Application;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Revenue-exposure concentration across the customer book (spec section 29). Read-only.</summary>
[Authorize(Policy = AuroraPermissions.CustomersRead)]
public class CustomersController : ModuleControllerBase
{
    public CustomersController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        var exposures = await Db.Customers.AsNoTracking()
            .Select(c => new CustomerExposure(c.Name, c.AnnualRevenueExposure))
            .ToListAsync(ct);

        var result = CustomerConcentrationCalculator.Analyze(exposures);
        var rows = result.Customers.Select(c => new CustomerRow(c.Name, c.Exposure, c.SharePct)).ToList();

        var model = new CustomersIndexViewModel(
            result.TotalExposure,
            result.TopCustomerSharePct,
            result.TopThreeSharePct,
            result.Hhi,
            ModuleFormatting.Humanize(result.Band.ToString()),
            ToneFor(result.Band),
            rows);

        return View(model);
    }

    private static string ToneFor(ConcentrationBand band) => band switch
    {
        ConcentrationBand.HighlyConcentrated => ModuleFormatting.ToneCritical,
        ConcentrationBand.ModeratelyConcentrated => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneHealthy,
    };
}

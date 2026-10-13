using Aurora.Modules.Audit.Application;
using Aurora.Modules.Customers.Application;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Data.Repositories;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        var customers = await Customers(ct);
        var result = CustomerConcentrationCalculator.Analyze(customers.Select(c => new CustomerExposure(c.Name, c.Exposure)).ToList());
        // Analyze orders customers by exposure, then name. The same order gives each row its customer id.
        var idsInRankOrder = customers
            .OrderByDescending(c => c.Exposure).ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => c.CustomerId).ToList();
        var rows = result.Customers.Select((c, i) => new CustomerRow(idsInRankOrder[i], c.Name, c.Exposure, c.SharePct)).ToList();

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

    /// <summary>Shows what one customer means for concentration: its share, its rank, and the book without it.</summary>
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        var impact = CustomerConcentrationCalculator.Impact(await Customers(ct), id);
        if (impact is null) return NotFound();

        return View(new CustomerDetailsViewModel(
            impact.CustomerId,
            impact.Name,
            impact.Exposure,
            impact.SharePct,
            impact.Rank,
            impact.CustomerCount,
            impact.Hhi,
            impact.HhiWithoutCustomer,
            ModuleFormatting.Humanize(impact.BandWithoutCustomer.ToString()),
            ToneFor(impact.BandWithoutCustomer)));
    }

    private async Task<IReadOnlyList<KeyedCustomerExposure>> Customers(CancellationToken ct) =>
        await Entities<Customer>().SelectAsync(
            c => new KeyedCustomerExposure(c.Id, c.Name, c.AnnualRevenueExposure), null, q => q.OrderBy(c => c.Name), ct);

    private static string ToneFor(ConcentrationBand band) => band switch
    {
        ConcentrationBand.HighlyConcentrated => ModuleFormatting.ToneCritical,
        ConcentrationBand.ModeratelyConcentrated => ModuleFormatting.ToneWarning,
        _ => ModuleFormatting.ToneHealthy,
    };
}

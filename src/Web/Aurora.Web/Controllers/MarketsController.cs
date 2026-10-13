using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Markets.Application;
using Aurora.Modules.Markets.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>Demand and price trend per market from recorded index observations (spec section 23).</summary>
[Authorize(Policy = AuroraPermissions.MarketsRead)]
public class MarketsController : ModuleControllerBase
{
    public MarketsController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexAsync(new MarketObservationForm { ObservedOn = TodayUtc() }, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.MarketsManage)]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(MarketsIndexViewModel.Form))] MarketObservationForm form, [FromServices] IValidator<MarketObservationForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(MarketsIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();
        if (!ModelState.IsValid) return View(nameof(Index), await BuildIndexAsync(form, ct));

        var observation = new MarketObservation(org.Id, form.Market, form.ObservedOn, form.DemandIndex, form.PriceIndex);
        Entities<MarketObservation>().Add(observation);
        RecordAudit(org, AuditActions.MarketObservationRecorded, nameof(MarketObservation), observation.Id.ToString());
        await Db.SaveChangesAsync(ct);

        Success($"Observation recorded for {observation.Market}.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<MarketsIndexViewModel> BuildIndexAsync(MarketObservationForm form, CancellationToken ct)
    {
        var observations = await Entities<MarketObservation>().ListAsync(null, null, ct);
        var rows = observations
            .GroupBy(o => o.Market, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var trend = MarketTrendCalculator.Evaluate(
                    group.Select(o => new MarketPoint(o.ObservedOnUtc, o.DemandIndex, o.PriceIndex)).ToList());
                return new MarketRow(
                    group.Key,
                    trend.ObservationCount,
                    group.Max(o => o.ObservedOnUtc),
                    trend.DemandChangePct,
                    trend.PriceChangePct,
                    ModuleFormatting.Humanize(trend.DemandDirection.ToString()));
            })
            .ToList();

        return new MarketsIndexViewModel(rows, form);
    }
}

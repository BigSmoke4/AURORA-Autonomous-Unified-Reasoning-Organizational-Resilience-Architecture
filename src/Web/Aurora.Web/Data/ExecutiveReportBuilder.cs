using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Assets.Application;
using Aurora.Modules.Contracts.Application;
using Aurora.Modules.Customers.Application;
using Aurora.Modules.Finance.Application;
using Aurora.Modules.Markets.Application;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Operations.Application;
using Aurora.Modules.Reporting.Application;
using Aurora.Modules.SupplyChain.Application;
using Aurora.Modules.Technology.Application;
using Aurora.Modules.Workforce.Application;
using Aurora.Web.Models.Modules;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

/// <summary>
/// Composes the executive report from the module calculators (spec section 30). Each figure comes from the same
/// deterministic calculator the module page uses, so the report and the pages cannot disagree. Reads are
/// tenant-filtered by the DbContext, so the report covers only the current organization.
/// </summary>
public sealed class ExecutiveReportBuilder
{
    public const string AttentionStatus = "Attention";
    public const string OkStatus = "OK";
    public const string NoPolicyStatus = "No policy";
    public const string NoReadingsStatus = "No readings";
    private const int RecentOperationsRecordLimit = 200;

    private readonly AuroraDbContext _db;
    private readonly TimeProvider _clock;

    public ExecutiveReportBuilder(AuroraDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ReportLine>> BuildAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var lines = new List<ReportLine>();

        await AddWorkforceAsync(lines, ct);
        await AddFinanceAsync(lines, ct);
        await AddOperationsAsync(lines, ct);
        await AddTechnologyAsync(lines, now, ct);
        await AddCustomersAsync(lines, ct);
        await AddMarketsAsync(lines, ct);
        await AddAssetsAsync(lines, now, ct);
        await AddContractsAsync(lines, now, ct);
        await AddMonitoringAsync(lines, ct);
        await AddAlertsAsync(lines, ct);
        await AddSupplyChainAsync(lines, ct);

        return lines;
    }

    private async Task AddWorkforceAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var roles = await _db.CriticalRoles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var headcount = (await _db.Employees.AsNoTracking()
                .GroupBy(e => e.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Role, x => x.Count, StringComparer.Ordinal);

        foreach (var role in roles)
        {
            var actual = headcount.GetValueOrDefault(role.RoleTitle);
            var coverage = WorkforceCoverageCalculator.Evaluate(role.RequiredHeadcount, actual);
            lines.Add(new ReportLine("Workforce", role.Name,
                $"{actual} of {role.RequiredHeadcount} ({ModuleFormatting.Percent(coverage.CoveragePct)})",
                ModuleFormatting.Humanize(coverage.Status.ToString())));
        }
    }

    private async Task AddFinanceAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var centers = await _db.CostCenters.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);
        foreach (var center in centers)
        {
            var position = BudgetPositionCalculator.Evaluate(center.AnnualBudget, center.ActualSpend);
            lines.Add(new ReportLine("Finance", $"{center.Code} {center.Name}",
                $"{ModuleFormatting.Money(center.ActualSpend)} spent of {ModuleFormatting.Money(center.AnnualBudget)}",
                ModuleFormatting.Humanize(position.Status.ToString())));
        }
    }

    private async Task AddOperationsAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var records = await _db.ProductionRecords.AsNoTracking()
            .OrderByDescending(r => r.PeriodStartUtc)
            .Take(RecentOperationsRecordLimit)
            .ToListAsync(ct);

        var evaluated = records
            .Select(r => ProductionPerformanceCalculator.Evaluate(r.PlannedUnits, r.ActualUnits, r.DefectiveUnits))
            .ToList();
        var behind = evaluated.Count(e => e.AttainmentStatus == AttainmentStatus.Behind);
        var belowYield = evaluated.Count(e => e.YieldStatus == YieldStatus.BelowTarget);

        lines.Add(new ReportLine("Operations", "Recent records behind plan", FormatCount(behind), StatusFor(behind)));
        lines.Add(new ReportLine("Operations", "Recent records below yield target", FormatCount(belowYield), StatusFor(belowYield)));
    }

    private async Task AddTechnologyAsync(List<ReportLine> lines, DateTime now, CancellationToken ct)
    {
        var assets = await _db.TechnologyAssets.AsNoTracking().OrderBy(a => a.Name).ToListAsync(ct);
        var policies = (await _db.TechnologyPatchPolicies.AsNoTracking().ToListAsync(ct))
            .ToDictionary(p => p.TechnologyAssetId);

        foreach (var asset in assets)
        {
            if (!policies.TryGetValue(asset.Id, out var policy))
            {
                lines.Add(new ReportLine("Technology", asset.Name, NoPolicyStatus, NoPolicyStatus));
                continue;
            }

            var compliance = PatchComplianceCalculator.Evaluate(policy.LastPatchedOnUtc, policy.PatchSlaDays, now);
            lines.Add(new ReportLine("Technology", asset.Name,
                $"{compliance.DaysSincePatch} days since patch, {compliance.DaysOverSla} days over SLA",
                ModuleFormatting.Humanize(compliance.Status.ToString())));
        }
    }

    private async Task AddCustomersAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var exposures = await _db.Customers.AsNoTracking()
            .Select(c => new CustomerExposure(c.Name, c.AnnualRevenueExposure))
            .ToListAsync(ct);
        var result = CustomerConcentrationCalculator.Analyze(exposures);

        lines.Add(new ReportLine("Customers", "Concentration (HHI)", ModuleFormatting.Number(result.Hhi),
            ModuleFormatting.Humanize(result.Band.ToString())));
        lines.Add(new ReportLine("Customers", "Largest customer share", ModuleFormatting.Percent(result.TopCustomerSharePct),
            ModuleFormatting.NotApplicable));
    }

    private async Task AddMarketsAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var observations = await _db.MarketObservations.AsNoTracking().ToListAsync(ct);
        foreach (var group in observations.GroupBy(o => o.Market, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var trend = MarketTrendCalculator.Evaluate(
                group.Select(o => new MarketPoint(o.ObservedOnUtc, o.DemandIndex, o.PriceIndex)).ToList());
            lines.Add(new ReportLine("Markets", group.Key,
                $"Demand {ModuleFormatting.Percent(trend.DemandChangePct)}, price {ModuleFormatting.Percent(trend.PriceChangePct)}",
                ModuleFormatting.Humanize(trend.DemandDirection.ToString())));
        }
    }

    private async Task AddAssetsAsync(List<ReportLine> lines, DateTime now, CancellationToken ct)
    {
        var assets = await _db.PhysicalAssets.AsNoTracking().ToListAsync(ct);
        var results = assets.Select(a => StraightLineDepreciationCalculator.Evaluate(
            a.AcquisitionCost, a.ResidualValue, a.UsefulLifeYears, a.InServiceOnUtc, now)).ToList();

        lines.Add(new ReportLine("Assets", "Total net book value", ModuleFormatting.Money(results.Sum(r => r.NetBookValue)),
            ModuleFormatting.NotApplicable));
        lines.Add(new ReportLine("Assets", "Fully depreciated assets", FormatCount(results.Count(r => r.FullyDepreciated)),
            ModuleFormatting.NotApplicable));
    }

    private async Task AddContractsAsync(List<ReportLine> lines, DateTime now, CancellationToken ct)
    {
        var contracts = await _db.SupplierContracts.AsNoTracking().OrderBy(c => c.EndsOnUtc).ToListAsync(ct);
        foreach (var contract in contracts)
        {
            var status = ContractStatusCalculator.Evaluate(contract.StartsOnUtc, contract.EndsOnUtc, contract.NoticePeriodDays, now);
            lines.Add(new ReportLine("Contracts", $"{contract.Reference} ({contract.Counterparty})",
                $"{status.DaysRemaining} days remaining",
                ModuleFormatting.Humanize(status.State.ToString())));
        }
    }

    private async Task AddMonitoringAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var definitions = await _db.MetricDefinitions.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        foreach (var definition in definitions)
        {
            var latest = await _db.MetricReadings.AsNoTracking()
                .Where(r => r.MetricDefinitionId == definition.Id)
                .OrderByDescending(r => r.RecordedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (latest is null)
            {
                lines.Add(new ReportLine("Monitoring", definition.Name, NoReadingsStatus, NoReadingsStatus));
                continue;
            }

            lines.Add(new ReportLine("Monitoring", definition.Name,
                $"{ModuleFormatting.Number(latest.Value)} {definition.Unit}",
                ModuleFormatting.Humanize(((ThresholdState)latest.ThresholdState).ToString())));
        }
    }

    private async Task AddAlertsAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var openBySeverity = (await _db.Alerts.AsNoTracking()
                .Where(a => a.Status != AlertStatus.Resolved)
                .GroupBy(a => a.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Severity, x => x.Count);

        foreach (var severity in Enum.GetValues<AlertSeverity>())
        {
            var count = openBySeverity.GetValueOrDefault(severity);
            lines.Add(new ReportLine("Alerts", $"Open {severity.ToString().ToLowerInvariant()} alerts", FormatCount(count), StatusFor(count)));
        }
    }

    private async Task AddSupplyChainAsync(List<ReportLine> lines, CancellationToken ct)
    {
        var positions = await _db.InventoryPositions.AsNoTracking().OrderBy(p => p.Sku).ThenBy(p => p.Location).ToListAsync(ct);
        foreach (var position in positions)
        {
            var coverage = SupplyCoverageCalculator.Evaluate(position.OnHandUnits, position.AverageDailyDemand,
                position.ReplenishmentLeadTimeDays, position.SafetyStockUnits);
            lines.Add(new ReportLine("Supply chain", $"{position.Sku} at {position.Location}",
                $"{position.OnHandUnits} on hand, reorder point {ModuleFormatting.Number(coverage.ReorderPointUnits)}",
                ModuleFormatting.Humanize(coverage.State.ToString())));
        }

        var suppliers = await _db.Suppliers.AsNoTracking().Select(s => s.CapacityUtilizationPct).ToListAsync(ct);
        var stretched = suppliers.Count(SupplierCapacityRule.IsStretched);
        lines.Add(new ReportLine("Supply chain", "Suppliers at or above capacity threshold", FormatCount(stretched), StatusFor(stretched)));
    }

    private static string FormatCount(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string StatusFor(int count) => count > 0 ? AttentionStatus : OkStatus;
}

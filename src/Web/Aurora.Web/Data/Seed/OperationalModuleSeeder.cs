using Aurora.Modules.Alerts.Application;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Assets.Domain;
using Aurora.Modules.Contracts.Domain;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Finance.Domain;
using Aurora.Modules.Markets.Domain;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Modules.Operations.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.SupplyChain.Domain;
using Aurora.Modules.Technology.Domain;
using Aurora.Modules.Workforce.Domain;

namespace Aurora.Web.Data.Seed;

/// <summary>
/// Adds demo records for the Phase 3 modules to the demo enterprise. Only inputs are stored; every figure a page
/// shows is computed by the module calculators at read time. Runs only inside the first-time demo seed, so
/// databases seeded before Phase 3 keep their existing data.
/// </summary>
public static class OperationalModuleSeeder
{
    private const int MonthsOfProductionHistory = 2;
    private const int ProductionPlannedUnits = 10_000;

    // Per-factory outcomes, applied in factory order: actual shortfall vs plan, and defective units.
    private static readonly (int Shortfall, int Defects)[] FactoryOutcomes = [(400, 150), (700, 60), (300, 90)];

    private const int MesPatchSlaDays = 30;
    private const int DatabasePatchSlaDays = 90;
    private const int CloudPatchSlaDays = 30;
    private const int MesLastPatchedDaysAgo = 20;
    private const int DatabaseLastPatchedDaysAgo = 120;
    private const int CloudLastPatchedDaysAgo = 10;

    private const int NorthbridgeNoticeDays = 90;
    private const int HalcyonNoticeDays = 60;
    private const int NorthbridgeStartedDaysAgo = 300;
    private const int HalcyonStartedDaysAgo = 100;
    private const int NorthbridgeEndsInDays = 60;
    private const int HalcyonEndsInDays = 500;

    private const int CncLifeYears = 10;
    private const int ForkliftLifeYears = 6;
    private const int CncInServiceDaysAgo = 1000;
    private const int ForkliftInServiceDaysAgo = 400;

    private const int ActuatorOnHandUnits = 120;
    private const decimal ActuatorDailyDemand = 15m;
    private const int ActuatorLeadTimeDays = 10;
    private const int ActuatorSafetyStock = 40;
    private const int PumpOnHandUnits = 0;
    private const decimal PumpDailyDemand = 8m;
    private const int PumpLeadTimeDays = 14;
    private const int PumpSafetyStock = 30;
    private const int McuOnHandUnits = 900;
    private const decimal McuDailyDemand = 20m;
    private const int McuLeadTimeDays = 7;
    private const int McuSafetyStock = 100;

    private const decimal ProductionBudget = 12_000_000m;
    private const decimal ProductionSpend = 11_250_000m;
    private const decimal ItBudget = 3_000_000m;
    private const decimal ItSpend = 1_200_000m;
    private const decimal ProcurementBudget = 900_000m;
    private const decimal ProcurementSpend = 960_000m;

    private const decimal IndexBase = 100m;
    private const int MarketHistoryDaysAgo = 60;
    private const int MarketMidDaysAgo = 30;

    private const decimal CoolantMinimum = 15m;
    private const decimal CoolantMaximum = 35m;
    private const decimal CoolantReading = 22m;
    private const decimal CycleTimeMaximum = 45m;
    private const decimal CycleTimeBreach = 52m;

    public static async Task SeedAsync(AuroraDbContext db, Organization org, IReadOnlyList<Factory> factories,
        IReadOnlyList<TechnologyAsset> techAssets, DateTime nowUtc)
    {
        var today = nowUtc.Date;

        db.CriticalRoles.AddRange(
            new CriticalRole(org.Id, "Plant operations continuity", "Plant Operations Manager", 1, 5),
            new CriticalRole(org.Id, "IT infrastructure cover", "IT Infrastructure Lead", 2, 4),
            new CriticalRole(org.Id, "Procurement leadership", "Procurement Director", 1, 4));

        db.CostCenters.AddRange(
            new CostCenter(org.Id, "PRD-100", "Plant production", ProductionBudget, ProductionSpend),
            new CostCenter(org.Id, "IT-200", "IT infrastructure", ItBudget, ItSpend),
            new CostCenter(org.Id, "PRC-300", "Procurement", ProcurementBudget, ProcurementSpend));

        var firstPeriod = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1 - MonthsOfProductionHistory);
        for (var index = 0; index < factories.Count; index++)
        {
            var factory = factories[index];
            var outcome = FactoryOutcomes[index % FactoryOutcomes.Length];
            for (var month = 0; month < MonthsOfProductionHistory; month++)
            {
                db.ProductionRecords.Add(new ProductionRecord(org.Id, factory.Id, firstPeriod.AddMonths(month),
                    ProductionPlannedUnits, ProductionPlannedUnits - outcome.Shortfall, outcome.Defects));
            }
        }

        db.TechnologyPatchPolicies.AddRange(
            new PatchPolicy(org.Id, techAssets[0].Id, today.AddDays(-MesLastPatchedDaysAgo), MesPatchSlaDays),
            new PatchPolicy(org.Id, techAssets[1].Id, today.AddDays(-DatabaseLastPatchedDaysAgo), DatabasePatchSlaDays),
            new PatchPolicy(org.Id, techAssets[2].Id, today.AddDays(-CloudLastPatchedDaysAgo), CloudPatchSlaDays));

        db.MarketObservations.AddRange(
            new MarketObservation(org.Id, "Industrial actuators", today.AddDays(-MarketHistoryDaysAgo), IndexBase, IndexBase),
            new MarketObservation(org.Id, "Industrial actuators", today.AddDays(-MarketMidDaysAgo), 104m, 102m),
            new MarketObservation(org.Id, "Industrial actuators", today, 101m, 97m),
            new MarketObservation(org.Id, "Hydraulic pumps", today.AddDays(-MarketHistoryDaysAgo), IndexBase, IndexBase),
            new MarketObservation(org.Id, "Hydraulic pumps", today.AddDays(-MarketMidDaysAgo), 112m, 103m),
            new MarketObservation(org.Id, "Hydraulic pumps", today, 118m, 106m));

        db.PhysicalAssets.AddRange(
            new PhysicalAsset(org.Id, "CNC machining centre", "Machinery", 2_400_000m, 240_000m, today.AddDays(-CncInServiceDaysAgo), CncLifeYears),
            new PhysicalAsset(org.Id, "Forklift fleet", "Vehicles", 180_000m, 0m, today.AddDays(-ForkliftInServiceDaysAgo), ForkliftLifeYears));

        db.SupplierContracts.AddRange(
            new SupplierContract(org.Id, "NB-2025-07", "Northbridge Components", today.AddDays(-NorthbridgeStartedDaysAgo),
                today.AddDays(NorthbridgeEndsInDays), 42_000_000m, 25_000m, NorthbridgeNoticeDays),
            new SupplierContract(org.Id, "HM-2026-02", "Halcyon Metals", today.AddDays(-HalcyonStartedDaysAgo),
                today.AddDays(HalcyonEndsInDays), 18_000_000m, 4_000m, HalcyonNoticeDays));

        db.InventoryPositions.AddRange(
            new InventoryPosition(org.Id, "IAC-7000", "Central Distribution Center", ActuatorOnHandUnits, ActuatorDailyDemand, ActuatorLeadTimeDays, ActuatorSafetyStock),
            new InventoryPosition(org.Id, "PHP-450", "Central Distribution Center", PumpOnHandUnits, PumpDailyDemand, PumpLeadTimeDays, PumpSafetyStock),
            new InventoryPosition(org.Id, "MCU-220", "West Coast Warehouse", McuOnHandUnits, McuDailyDemand, McuLeadTimeDays, McuSafetyStock));

        SeedMonitoring(db, org, nowUtc);

        await db.SaveChangesAsync();
    }

    private static void SeedMonitoring(AuroraDbContext db, Organization org, DateTime nowUtc)
    {
        var cycleTime = new MetricDefinition(org.Id, "Line 1 cycle time", "min", null, CycleTimeMaximum);
        var coolant = new MetricDefinition(org.Id, "Coolant temperature", "deg C", CoolantMinimum, CoolantMaximum);
        db.MetricDefinitions.AddRange(cycleTime, coolant);

        AddReading(db, org, cycleTime, CycleTimeBreach, nowUtc);
        AddReading(db, org, coolant, CoolantReading, nowUtc);

        db.Alerts.Add(new Alert(org.Id, AlertSourceKeys.ForMetric(cycleTime.Id), "Line 1 cycle time above maximum",
            $"Latest reading {CycleTimeBreach} min is above the {CycleTimeMaximum} min maximum.",
            AlertSeverity.Warning, nowUtc));
    }

    private static void AddReading(AuroraDbContext db, Organization org, MetricDefinition definition, decimal value, DateTime nowUtc)
    {
        var state = MetricThresholdEvaluator.Evaluate(value, definition.MinimumThreshold, definition.MaximumThreshold);
        db.MetricReadings.Add(new MetricReading(org.Id, definition.Id, value, nowUtc, (int)state));
    }
}

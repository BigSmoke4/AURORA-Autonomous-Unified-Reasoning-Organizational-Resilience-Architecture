using Aurora.Shared.Kernel;

namespace Aurora.Modules.SupplyChain.Domain;

/// <summary>On-hand stock for one SKU at one location, with the replenishment parameters that drive it (spec section 26).</summary>
public class InventoryPosition : Entity, ITenantScoped
{
    public const int SkuMaxLength = 60;
    public const int LocationMaxLength = 200;
    public const int MaxLeadTimeDays = 365;

    public Guid OrganizationId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public int OnHandUnits { get; private set; }
    public decimal AverageDailyDemand { get; private set; }
    public int ReplenishmentLeadTimeDays { get; private set; }
    public int SafetyStockUnits { get; private set; }

    protected InventoryPosition()
    {
    }

    public InventoryPosition(Guid organizationId, string sku, string location, int onHandUnits, decimal averageDailyDemand, int replenishmentLeadTimeDays, int safetyStockUnits)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Sku = DomainGuard.RequireText(sku, SkuMaxLength, nameof(sku));
        Location = DomainGuard.RequireText(location, LocationMaxLength, nameof(location));
        OnHandUnits = DomainGuard.RequireRange(onHandUnits, 0, int.MaxValue, nameof(onHandUnits));
        AverageDailyDemand = DomainGuard.RequireNonNegative(averageDailyDemand, nameof(averageDailyDemand));
        ReplenishmentLeadTimeDays = DomainGuard.RequireRange(replenishmentLeadTimeDays, 0, MaxLeadTimeDays, nameof(replenishmentLeadTimeDays));
        SafetyStockUnits = DomainGuard.RequireRange(safetyStockUnits, 0, int.MaxValue, nameof(safetyStockUnits));
    }
}

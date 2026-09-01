using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

/// <summary>A supplier in the enterprise digital twin. CapacityUtilizationPct and
/// RevenueExposure feed the deterministic simulation engine directly — the simulation never
/// invents these figures, it reads them from here (spec section 74).</summary>
public class Supplier : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string? Location { get; private set; }

    /// <summary>0-100. How much of this supplier's contracted capacity is currently used.</summary>
    public decimal CapacityUtilizationPct { get; private set; }

    /// <summary>1 (low) - 5 (critical). Used directly by the Risk scoring formula.</summary>
    public int Criticality { get; private set; } = 3;

    /// <summary>Annual revenue (USD) that depends, directly or indirectly, on this supplier
    /// staying operational. Seeded from realistic figures; used as the financial basis for
    /// simulation impact calculations rather than an LLM-estimated number.</summary>
    public decimal AnnualRevenueExposure { get; private set; }

    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected Supplier() { }

    public Supplier(Guid organizationId, string name, string category, decimal capacityUtilizationPct,
        int criticality, decimal annualRevenueExposure, string? location = null)
    {
        OrganizationId = organizationId;
        Name = name;
        Category = category;
        CapacityUtilizationPct = capacityUtilizationPct;
        Criticality = Math.Clamp(criticality, 1, 5);
        AnnualRevenueExposure = annualRevenueExposure;
        Location = location;
    }

    public void ApplyCapacityShock(decimal reductionPct)
    {
        CapacityUtilizationPct = Math.Clamp(CapacityUtilizationPct - reductionPct, 0, 100);
        Status = CapacityUtilizationPct < 40 ? OperationalStatus.Critical
            : CapacityUtilizationPct < 70 ? OperationalStatus.Warning
            : OperationalStatus.Healthy;
        Touch();
    }
}

using Aurora.Shared.Kernel;

namespace Aurora.Modules.Operations.Domain;

/// <summary>Planned and actual output for one factory over one period (spec section 21).</summary>
public class ProductionRecord : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid FactoryId { get; private set; }
    public DateTime PeriodStartUtc { get; private set; }
    public int PlannedUnits { get; private set; }
    public int ActualUnits { get; private set; }
    public int DefectiveUnits { get; private set; }

    protected ProductionRecord()
    {
    }

    public ProductionRecord(Guid organizationId, Guid factoryId, DateTime periodStartUtc, int plannedUnits, int actualUnits, int defectiveUnits)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        FactoryId = DomainGuard.RequireId(factoryId, nameof(factoryId));
        PeriodStartUtc = DomainGuard.ToUtc(periodStartUtc);
        PlannedUnits = DomainGuard.RequireRange(plannedUnits, 1, int.MaxValue, nameof(plannedUnits));
        ActualUnits = DomainGuard.RequireRange(actualUnits, 0, int.MaxValue, nameof(actualUnits));
        DefectiveUnits = DomainGuard.RequireRange(defectiveUnits, 0, actualUnits, nameof(defectiveUnits));
    }
}

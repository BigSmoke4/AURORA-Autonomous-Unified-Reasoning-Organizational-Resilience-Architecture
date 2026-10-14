using Aurora.Shared.Kernel;

namespace Aurora.Modules.Monitoring.Domain;

/// <summary>A monitored measure with optional lower and upper thresholds (spec section 27).</summary>
public class MetricDefinition : Entity, ITenantScoped
{
    public const int NameMaxLength = 200;
    public const int UnitMaxLength = 30;

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal? MinimumThreshold { get; private set; }
    public decimal? MaximumThreshold { get; private set; }

    protected MetricDefinition()
    {
    }

    public MetricDefinition(Guid organizationId, string name, string unit, decimal? minimumThreshold, decimal? maximumThreshold)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Name = DomainGuard.RequireText(name, NameMaxLength, nameof(name));
        Unit = DomainGuard.RequireText(unit, UnitMaxLength, nameof(unit));
        if (minimumThreshold is not null && maximumThreshold is not null && minimumThreshold >= maximumThreshold)
            throw new ArgumentException("The minimum threshold must be below the maximum threshold.", nameof(minimumThreshold));
        if (minimumThreshold is null && maximumThreshold is null)
            throw new ArgumentException("At least one threshold is required.", nameof(minimumThreshold));
        MinimumThreshold = minimumThreshold;
        MaximumThreshold = maximumThreshold;
    }
}

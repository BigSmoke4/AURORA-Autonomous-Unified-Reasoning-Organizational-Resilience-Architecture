using Aurora.Shared.Kernel;

namespace Aurora.Modules.Monitoring.Domain;

/// <summary>One observed value for a metric, with the threshold state it was judged against at ingestion.</summary>
public class MetricReading : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid MetricDefinitionId { get; private set; }
    public decimal Value { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public int ThresholdState { get; private set; }

    protected MetricReading()
    {
    }

    public MetricReading(Guid organizationId, Guid metricDefinitionId, decimal value, DateTime recordedAtUtc, int thresholdState)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        MetricDefinitionId = DomainGuard.RequireId(metricDefinitionId, nameof(metricDefinitionId));
        Value = value;
        RecordedAtUtc = DomainGuard.ToUtc(recordedAtUtc);
        ThresholdState = thresholdState;
    }
}

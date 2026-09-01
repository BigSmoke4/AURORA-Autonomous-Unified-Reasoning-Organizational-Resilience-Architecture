using Aurora.Shared.Kernel;

namespace Aurora.Modules.Scenarios.Domain;

/// <summary>
/// A user- or AI-drafted hypothesis to run through the Simulation engine (spec section 15/16).
/// The Scenario itself carries no calculation logic — it is pure configuration; the Simulation
/// module reads it and does the math (spec section 13 architecture: Scenario → Variables →
/// Simulation Configuration → ... → Simulation Results).
/// </summary>
public class Scenario : AggregateRoot, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid TargetNodeId { get; private set; }
    public ScenarioEventType EventType { get; private set; }

    /// <summary>0.0-1.0. E.g. 0.6 = a 60% capacity reduction.</summary>
    public decimal Severity { get; private set; }

    public int DurationDays { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public string? Assumptions { get; private set; }
    public ScenarioStatus Status { get; private set; } = ScenarioStatus.Draft;
    public Guid CreatedByUserId { get; private set; }

    /// <summary>True if this scenario was drafted from a natural-language request via the
    /// rule-based extractor (spec section 16) rather than built directly in the Scenario Lab form.</summary>
    public bool CreatedFromNaturalLanguage { get; private set; }

    protected Scenario() { }

    public Scenario(Guid organizationId, string name, Guid targetNodeId, ScenarioEventType eventType,
        decimal severity, int durationDays, DateTime startDateUtc, Guid createdByUserId,
        string? assumptions = null, bool createdFromNaturalLanguage = false)
    {
        OrganizationId = organizationId;
        Name = name;
        TargetNodeId = targetNodeId;
        EventType = eventType;
        Severity = Math.Clamp(severity, 0m, 1m);
        DurationDays = Math.Max(1, durationDays);
        StartDateUtc = startDateUtc;
        CreatedByUserId = createdByUserId;
        Assumptions = assumptions;
        CreatedFromNaturalLanguage = createdFromNaturalLanguage;
        Status = ScenarioStatus.Ready;
    }

    public void MarkRunning() { Status = ScenarioStatus.Running; Touch(); }
    public void MarkCompleted() { Status = ScenarioStatus.Completed; Touch(); }
    public void MarkFailed() { Status = ScenarioStatus.Failed; Touch(); }
}

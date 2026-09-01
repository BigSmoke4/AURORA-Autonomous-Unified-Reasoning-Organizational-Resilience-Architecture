using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

public enum InvestigationStatus { Running = 0, Completed = 1, Failed = 2 }

/// <summary>
/// One user question answered by composing real tool calls (spec section 30: AI Investigation
/// Trace). Every AiEvidence row attached to this investigation was produced by a deterministic
/// service (GraphTraversalService, RiskScoringService, SimulationEngine) — the AiInvestigation
/// record itself only stores which tools ran, in what order, and the narrative explanation.
/// </summary>
public class AiInvestigation : AggregateRoot, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public InvestigationStatus Status { get; private set; } = InvestigationStatus.Running;
    public string? NarrativeExplanation { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }

    private readonly List<AiEvidence> _evidence = new();
    public IReadOnlyList<AiEvidence> Evidence => _evidence.AsReadOnly();

    protected AiInvestigation() { }

    public AiInvestigation(Guid organizationId, Guid requestedByUserId, string question)
    {
        OrganizationId = organizationId;
        RequestedByUserId = requestedByUserId;
        Question = question;
    }

    public void AddEvidence(string toolName, string summary, string rawDataJson, TimeSpan duration)
        => _evidence.Add(new AiEvidence(Id, toolName, summary, rawDataJson, duration));

    public void Complete(string narrativeExplanation, decimal confidence)
    {
        NarrativeExplanation = narrativeExplanation;
        Confidence = Math.Clamp(confidence, 0, 100);
        Status = InvestigationStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Fail()
    {
        Status = InvestigationStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        Touch();
    }
}

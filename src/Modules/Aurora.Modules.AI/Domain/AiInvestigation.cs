using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

public enum InvestigationStatus { Running = 0, Completed = 1, Failed = 2 }

/// <summary>Who wrote the narrative. Deterministic text is always available and is the fallback.</summary>
public enum NarrativeSource { Deterministic = 0, Llm = 1 }

/// <summary>
/// One user question answered by composing real tool calls (spec section 30: AI Investigation
/// Trace). Every AiEvidence row was produced by a deterministic service. The record stores which
/// tools ran, the narrative, and which model (if any) wrote that narrative.
/// </summary>
public class AiInvestigation : AggregateRoot, ITenantScoped
{
    public const int MaxQuestionLength = 1000;
    public const int MaxModelVersionLength = 60;

    public Guid OrganizationId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public InvestigationStatus Status { get; private set; } = InvestigationStatus.Running;
    public string? NarrativeExplanation { get; private set; }
    public NarrativeSource NarrativeSource { get; private set; } = NarrativeSource.Deterministic;
    public string ModelVersion { get; private set; } = string.Empty;
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
        Question = question.Length <= MaxQuestionLength ? question : question[..MaxQuestionLength];
    }

    public void AddEvidence(string toolName, string summary, string rawDataJson, TimeSpan duration)
        => _evidence.Add(new AiEvidence(Id, toolName, summary, rawDataJson, duration));

    public void Complete(string narrativeExplanation, decimal confidence, NarrativeSource source, string modelVersion)
    {
        NarrativeExplanation = narrativeExplanation;
        Confidence = Math.Clamp(confidence, 0m, 100m);
        NarrativeSource = source;
        ModelVersion = modelVersion.Length <= MaxModelVersionLength ? modelVersion : modelVersion[..MaxModelVersionLength];
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

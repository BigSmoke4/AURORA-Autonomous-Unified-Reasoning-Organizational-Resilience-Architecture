using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

/// <summary>One piece of evidence gathered by a real tool call during an investigation
/// (spec section 26: "every important recommendation should contain evidence").</summary>
public class AiEvidence : Entity
{
    public Guid AiInvestigationId { get; private set; }
    public string ToolName { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string RawDataJson { get; private set; } = string.Empty;
    public TimeSpan Duration { get; private set; }

    protected AiEvidence() { }

    public AiEvidence(Guid aiInvestigationId, string toolName, string summary, string rawDataJson, TimeSpan duration)
    {
        AiInvestigationId = aiInvestigationId;
        ToolName = toolName;
        Summary = summary;
        RawDataJson = rawDataJson;
        Duration = duration;
    }
}

using Aurora.Shared.Kernel;

namespace Aurora.Modules.Analysis.Domain;

public enum AnalysisKind { Counterfactual = 1, Optimization = 2, Forecast = 3 }

/// <summary>
/// A persisted, reproducible analysis run. It stores the exact inputs and the exact deterministic
/// outputs, plus the model version that produced them, so any number shown to a user can be
/// re-derived later and cited by a decision (spec section 76: model versioning).
/// </summary>
public class AnalysisRecord : Entity, ITenantScoped
{
    public const int MaxTitleLength = 200;
    public const int MaxModelVersionLength = 60;

    public Guid OrganizationId { get; private set; }
    public AnalysisKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }

    protected AnalysisRecord() { }

    public AnalysisRecord(Guid organizationId, AnalysisKind kind, string title, string modelVersion,
        string inputJson, string resultJson, Guid createdByUserId)
    {
        OrganizationId = organizationId;
        Kind = kind;
        Title = title;
        ModelVersion = modelVersion;
        InputJson = inputJson;
        ResultJson = resultJson;
        CreatedByUserId = createdByUserId;
    }
}

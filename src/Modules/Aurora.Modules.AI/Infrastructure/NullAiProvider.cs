using Aurora.Modules.AI.Application;

namespace Aurora.Modules.AI.Infrastructure;

/// <summary>
/// Used when no LLM provider is configured. Spec section 71 requires that the core keep working
/// without AI, so investigations still run on real evidence and use the deterministic narrative.
/// </summary>
public sealed class NullAiProvider : IAiProvider
{
    public const string Identity = "none";

    public bool IsConfigured => false;

    public string ModelVersion => Identity;

    public Task<NarrativeResult?> TryExplainAsync(Guid organizationId, string systemPrompt, string evidenceJson, CancellationToken ct = default) =>
        Task.FromResult<NarrativeResult?>(null);
}

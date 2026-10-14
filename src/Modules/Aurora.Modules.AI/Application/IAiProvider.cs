namespace Aurora.Modules.AI.Application;

/// <summary>A narrative produced by a model, with the model identity recorded for audit.</summary>
public sealed record NarrativeResult(string Text, string ModelVersion);

/// <summary>
/// Abstraction over an external LLM (spec sections 25 and 45). The model never has database or tool
/// access. It receives only text that AURORA has already assembled from deterministic evidence, and
/// it returns prose. It is never asked to calculate a number.
/// </summary>
public interface IAiProvider
{
    bool IsConfigured { get; }

    /// <summary>Identity of the model behind this provider, recorded on every investigation.</summary>
    string ModelVersion { get; }

    /// <summary>
    /// Returns a narrative, or null when the provider is not configured, is unavailable, times out,
    /// or returns nothing usable. Callers then fall back to the deterministic narrative (spec section 71).
    /// The organization scopes any cache, so one tenant never reads or probes another tenant's narrative.
    /// </summary>
    Task<NarrativeResult?> TryExplainAsync(Guid organizationId, string systemPrompt, string evidenceJson, CancellationToken ct = default);
}

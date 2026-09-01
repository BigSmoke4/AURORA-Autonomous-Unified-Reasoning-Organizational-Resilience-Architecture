using Aurora.Modules.AI.Application;

namespace Aurora.Modules.AI.Infrastructure;

/// <summary>
/// Default AI provider when no external LLM API key is configured. Spec section 71
/// (resilience): "If the LLM provider goes offline, core digital twin, graph, simulation,
/// risk, and reporting remain operational." This provider makes that true even on day one —
/// investigations still run and return real, evidence-backed results; only the free-text
/// narrative explanation is replaced with a plain templated summary of the same evidence.
/// </summary>
public sealed class NullAiProvider : IAiProvider
{
    public bool IsConfigured => false;

    public Task<string> ExplainAsync(string systemPrompt, string evidenceJson, CancellationToken ct = default)
    {
        return Task.FromResult(
            "AI narrative explanation is unavailable because no LLM provider is configured " +
            "(set AI:Provider and AI:ApiKey in configuration to enable one). The evidence below " +
            "was still computed deterministically by the graph and simulation engines and is fully valid.");
    }
}

namespace Aurora.Modules.AI.Application;

/// <summary>
/// Abstraction over an external LLM (spec section 25/45: the model is never given direct
/// database or tool access — it only ever receives text AURORA has already assembled from
/// real evidence, and returns text). Swap NullAiProvider for a real OpenAI/Anthropic-backed
/// implementation by registering a different IAiProvider in Aurora.Web/Program.cs; nothing
/// else in the AI module needs to change.
/// </summary>
public interface IAiProvider
{
    bool IsConfigured { get; }

    /// <summary>Produces a natural-language explanation of evidence that has ALREADY been
    /// computed deterministically. The provider must not be asked to calculate numbers.</summary>
    Task<string> ExplainAsync(string systemPrompt, string evidenceJson, CancellationToken ct = default);
}

using Aurora.Modules.AI.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Aurora.Web.Infrastructure;

/// <summary>
/// Reports whether an LLM provider is configured. A missing provider is <c>Degraded</c>, not
/// <c>Unhealthy</c>: spec section 71 requires the deterministic core to keep working without AI.
/// </summary>
public sealed class AiProviderHealthCheck : IHealthCheck
{
    private readonly AiOptions _options;

    public AiProviderHealthCheck(IOptions<AiOptions> options) => _options = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = _options.IsLlmProviderConfigured
            ? HealthCheckResult.Healthy($"LLM provider '{_options.Provider}' is configured.")
            : HealthCheckResult.Degraded("No LLM provider configured. Graph, simulation, risk and reporting remain fully available.");
        return Task.FromResult(result);
    }
}

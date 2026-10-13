using Aurora.Modules.Simulation.Application;
using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Hubs;

/// <summary>
/// Sends simulation progress to the scenario's SignalR group. Progress is best-effort: a failed
/// push is logged and never changes the outcome of the simulation run, which is saved regardless.
/// </summary>
public sealed class SignalRSimulationProgressNotifier : ISimulationProgressNotifier
{
    internal const string ProgressMethod = "progress";

    private readonly IHubContext<SimulationHub> _hub;
    private readonly ILogger<SignalRSimulationProgressNotifier> _logger;

    public SignalRSimulationProgressNotifier(IHubContext<SimulationHub> hub, ILogger<SignalRSimulationProgressNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task ReportProgressAsync(
        Guid organizationId,
        Guid scenarioId,
        int completedPhases,
        int totalPhases,
        string message,
        CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(SimulationHub.GroupName(organizationId, scenarioId)).SendAsync(
                ProgressMethod,
                new { scenarioId, completedPhases, totalPhases, message },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Simulation progress could not be delivered for scenario {ScenarioId}.", scenarioId);
        }
    }
}

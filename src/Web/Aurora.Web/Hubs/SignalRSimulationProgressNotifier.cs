using Aurora.Modules.Simulation.Application;
using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Hubs;

public sealed class SignalRSimulationProgressNotifier : ISimulationProgressNotifier
{
    private readonly IHubContext<SimulationHub> _hub;
    public SignalRSimulationProgressNotifier(IHubContext<SimulationHub> hub) => _hub = hub;

    public Task ReportProgressAsync(Guid simulationRunId, int currentDay, int totalDays, string message, CancellationToken ct = default) =>
        _hub.Clients.Group(SimulationHub.GroupName(simulationRunId.ToString())).SendAsync("progress", new
        {
            simulationRunId,
            currentDay,
            totalDays,
            message
        }, ct);
}

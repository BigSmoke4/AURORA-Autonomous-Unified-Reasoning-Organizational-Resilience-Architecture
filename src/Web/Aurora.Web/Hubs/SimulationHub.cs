using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Hubs;

/// <summary>Real-time channel for simulation progress (spec section 39/61). Clients join a
/// group named after the simulation run id so progress only reaches the browser that started it
/// (and anyone else watching the same run, e.g. a supervisor).</summary>
public class SimulationHub : Hub
{
    public async Task JoinRun(string simulationRunId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(simulationRunId));

    public async Task LeaveRun(string simulationRunId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(simulationRunId));

    public static string GroupName(string simulationRunId) => $"sim-{simulationRunId}";
}

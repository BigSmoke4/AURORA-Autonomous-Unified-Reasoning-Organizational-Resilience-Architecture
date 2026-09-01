namespace Aurora.Modules.Simulation.Application;

/// <summary>Implemented in Aurora.Web via SignalR (spec section 39/61: real-time progress in
/// the Scenario Lab). The engine itself has no dependency on SignalR — it just reports progress
/// through this abstraction, so it stays testable without a hub.</summary>
public interface ISimulationProgressNotifier
{
    Task ReportProgressAsync(Guid simulationRunId, int currentDay, int totalDays, string message, CancellationToken ct = default);
}

public sealed class NullSimulationProgressNotifier : ISimulationProgressNotifier
{
    public Task ReportProgressAsync(Guid simulationRunId, int currentDay, int totalDays, string message, CancellationToken ct = default)
        => Task.CompletedTask;
}

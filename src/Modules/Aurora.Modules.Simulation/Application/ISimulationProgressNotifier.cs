namespace Aurora.Modules.Simulation.Application;

/// <summary>
/// Reports coarse, phase-based progress for a simulation run (spec section 39/61). Phases are real
/// steps of the run (snapshot loaded, propagation finished, results saved), not simulated days.
/// Implemented in Aurora.Web over SignalR. The engine does not depend on it. Progress is addressed
/// to one organization's scenario, so no message can reach another tenant.
/// </summary>
public interface ISimulationProgressNotifier
{
    Task ReportProgressAsync(
        Guid organizationId,
        Guid scenarioId,
        int completedPhases,
        int totalPhases,
        string message,
        CancellationToken ct = default);
}

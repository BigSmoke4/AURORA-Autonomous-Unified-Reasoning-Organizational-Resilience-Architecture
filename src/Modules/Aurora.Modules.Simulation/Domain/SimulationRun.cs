using Aurora.Shared.Kernel;

namespace Aurora.Modules.Simulation.Domain;

public enum SimulationRunStatus { Queued = 0, Running = 1, Completed = 2, Failed = 3 }

/// <summary>
/// A single execution of the deterministic simulation engine against a Scenario. Stores
/// everything needed to reproduce the run later (spec section 77): scenario id, graph snapshot
/// timestamp, model version, and a random seed even though the current model has no stochastic
/// step (kept for forward compatibility with Monte Carlo variants).
/// </summary>
public class SimulationRun : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid ScenarioId { get; private set; }
    public SimulationRunStatus Status { get; private set; } = SimulationRunStatus.Queued;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int RandomSeed { get; private set; }
    public string ModelVersion { get; private set; } = "deterministic-propagation-v1";
    public string? ErrorMessage { get; private set; }

    protected SimulationRun() { }

    public SimulationRun(Guid organizationId, Guid scenarioId, int randomSeed)
    {
        OrganizationId = organizationId;
        ScenarioId = scenarioId;
        RandomSeed = randomSeed;
    }

    public void Start() { Status = SimulationRunStatus.Running; StartedAtUtc = DateTime.UtcNow; Touch(); }
    public void Complete() { Status = SimulationRunStatus.Completed; CompletedAtUtc = DateTime.UtcNow; Touch(); }
    public void Fail(string error) { Status = SimulationRunStatus.Failed; ErrorMessage = error; CompletedAtUtc = DateTime.UtcNow; Touch(); }
}

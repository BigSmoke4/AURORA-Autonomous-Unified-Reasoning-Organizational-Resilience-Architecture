using Aurora.Shared.Kernel;

namespace Aurora.Modules.Simulation.Domain;

/// <summary>The aggregated summary of a completed run (spec section 62 Impact Summary).</summary>
public class SimulationResult : Entity
{
    public Guid SimulationRunId { get; private set; }
    public decimal TotalRevenueImpact { get; private set; }
    public decimal PeakDailyImpactScore { get; private set; }
    public int AffectedNodeCount { get; private set; }
    public int RecoveryTimeDays { get; private set; }
    public string Summary { get; private set; } = string.Empty;

    protected SimulationResult() { }

    public SimulationResult(Guid simulationRunId, decimal totalRevenueImpact, decimal peakDailyImpactScore,
        int affectedNodeCount, int recoveryTimeDays, string summary)
    {
        SimulationRunId = simulationRunId;
        TotalRevenueImpact = totalRevenueImpact;
        PeakDailyImpactScore = peakDailyImpactScore;
        AffectedNodeCount = affectedNodeCount;
        RecoveryTimeDays = recoveryTimeDays;
        Summary = summary;
    }
}

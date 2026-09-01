using Aurora.Shared.Kernel;

namespace Aurora.Modules.Simulation.Domain;

/// <summary>One node's impact reading on one simulated day. This is the raw time series the
/// Simulation Lab charts (spec section 62: Day 0 / 10 / 20 / 30 / 60 / 90).</summary>
public class SimulationStep : Entity
{
    public Guid SimulationRunId { get; private set; }
    public int DayNumber { get; private set; }
    public Guid NodeId { get; private set; }

    /// <summary>0.0-1.0 fraction of this node's capacity/output that is impaired on this day.</summary>
    public decimal ImpactScore { get; private set; }

    public string Status { get; private set; } = "Healthy";
    public decimal RevenueImpact { get; private set; }

    protected SimulationStep() { }

    public SimulationStep(Guid simulationRunId, int dayNumber, Guid nodeId, decimal impactScore, string status, decimal revenueImpact)
    {
        SimulationRunId = simulationRunId;
        DayNumber = dayNumber;
        NodeId = nodeId;
        ImpactScore = impactScore;
        Status = status;
        RevenueImpact = revenueImpact;
    }
}

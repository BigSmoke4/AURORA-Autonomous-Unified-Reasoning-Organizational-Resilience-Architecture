using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Domain;

namespace Aurora.Web.Models.Simulation;

public class ScenarioDetailsViewModel
{
    public Scenario Scenario { get; set; } = null!;
    public string TargetNodeName { get; set; } = string.Empty;
    public List<SimulationRun> PastRuns { get; set; } = new();
}

public class SimulationResultsViewModel
{
    public SimulationRun Run { get; set; } = null!;
    public SimulationResult? Result { get; set; }
    public Scenario Scenario { get; set; } = null!;
    public string TargetNodeName { get; set; } = string.Empty;

    /// <summary>Total impact score per day, summed across all affected nodes — the series the
    /// results chart plots (spec section 62: Day 0 / 10 / 20 / 30 / 60 / 90).</summary>
    public List<(int Day, decimal TotalImpactScore, decimal RevenueImpact)> DailySeries { get; set; } = new();

    public List<(string NodeName, decimal PeakImpact, decimal TotalRevenueImpact)> TopAffectedNodes { get; set; } = new();
}

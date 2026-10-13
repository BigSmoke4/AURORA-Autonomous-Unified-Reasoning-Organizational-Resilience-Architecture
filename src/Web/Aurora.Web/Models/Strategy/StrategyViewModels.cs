using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Web.Models.Strategy;

public sealed class StrategyIndexViewModel
{
    public IReadOnlyList<Scenario> Scenarios { get; init; } = Array.Empty<Scenario>();
    public CounterfactualForm Counterfactual { get; set; } = new();
    public OptimizationForm Optimization { get; set; } = new();
}

public sealed class CounterfactualForm
{
    public Guid ScenarioId { get; set; }

    /// <summary>Share of the target's downstream dependence moved to alternate sources, in percent.</summary>
    public int MitigationSharePct { get; set; } = 50;
}

public sealed class OptimizationForm
{
    public const int MaxCandidateLines = 50;
    public const int MaxCandidateTextLength = 6000;
    public decimal Demand { get; set; } = 1000m;

    /// <summary>Weight on cost, in percent. The remainder is weighted on risk.</summary>
    public int CostWeightPct { get; set; } = 50;
    public string CandidatesText { get; set; } =
        "Northbridge Components | 800 | 12.50 | 0.30 | 0.60\n" +
        "Vantage Electronics | 600 | 14.00 | 0.10 | 0.50\n" +
        "Halcyon Metals | 500 | 11.00 | 0.45 | 0.40";
}

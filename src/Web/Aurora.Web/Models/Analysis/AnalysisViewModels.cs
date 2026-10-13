using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Forecasting.Domain;
using Aurora.Modules.Optimization.Domain;
using Aurora.Modules.Simulation.Application;

namespace Aurora.Web.Models.Analysis;

public sealed class AnalysisDetailsViewModel
{
    public required AnalysisRecord Record { get; init; }
    public CounterfactualComparison? Counterfactual { get; init; }
    public OptimizationResult? Optimization { get; init; }
    public ForecastResult? Forecast { get; init; }
    public bool CanProposeDecision { get; init; }
    public Guid? ExistingDecisionId { get; init; }
}

public sealed class ProposeDecisionForm
{
    public Guid AnalysisRecordId { get; set; }
    public decimal OperationalFeasibility { get; set; } = 50m;
    public decimal StrategicAlignment { get; set; } = 50m;
    public int DaysToBenefit { get; set; } = 90;
    public decimal Confidence { get; set; } = 50m;
}

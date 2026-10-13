namespace Aurora.Modules.Optimization.Domain;

/// <summary>A source that can supply units. MaxShare caps this source's share of total demand.</summary>
public sealed record AllocationCandidate(
    string Id,
    string Name,
    decimal Capacity,
    decimal UnitCost,
    decimal UnitRisk,
    decimal MaxShare);

public sealed record AllocationLine(string CandidateId, string Name, decimal Units, decimal Cost, decimal RiskExposure);

public sealed record AllocationPlan(
    decimal CostWeight,
    decimal RiskWeight,
    IReadOnlyList<AllocationLine> Lines,
    decimal TotalUnits,
    decimal TotalCost,
    decimal TotalRiskExposure);

public sealed record OptimizationResult(
    string ModelVersion,
    decimal Demand,
    AllocationPlan Recommended,
    IReadOnlyList<AllocationPlan> ParetoFrontier);

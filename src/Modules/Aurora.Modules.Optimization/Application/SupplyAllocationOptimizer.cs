using Aurora.Modules.Optimization.Domain;

namespace Aurora.Modules.Optimization.Application;

public sealed class InfeasibleAllocationException(string message) : Exception(message);

public sealed class InvalidOptimizationRequestException(string message) : Exception(message);

/// <summary>
/// Deterministic supply allocation (spec section 16). Model: meet a single demand D from candidate
/// sources, each with a capacity, a per-unit cost and a per-unit risk exposure, and a cap on its
/// share of D. Cost and risk are min-max normalized across candidates, so both scales are
/// comparable and the weighted objective is linear.
///
/// For one demand constraint with per-source caps, filling sources in ascending order of weighted
/// per-unit score is optimal for that linear objective. The Pareto frontier comes from sweeping the
/// cost weight over a fixed grid. Only supported (convex-hull) points can be found this way, so the
/// frontier is an approximation and is labelled as such.
/// </summary>
public static class SupplyAllocationOptimizer
{
    public const string ModelVersion = "allocation-greedy-v1";
    public const int MaxCandidates = 50;
    public const int WeightGridSteps = 10;

    private const int UnitDecimals = 4;
    private const int CurrencyDecimals = 2;

    public static OptimizationResult Optimize(decimal demand, IReadOnlyList<AllocationCandidate> candidates, decimal costWeight)
    {
        Validate(demand, candidates, costWeight);

        var recommended = Solve(demand, candidates, costWeight);

        var sweep = new List<AllocationPlan>();
        for (var step = 0; step <= WeightGridSteps; step++)
        {
            var weight = (decimal)step / WeightGridSteps;
            sweep.Add(Solve(demand, candidates, weight));
        }

        return new OptimizationResult(ModelVersion, demand, recommended, ParetoFilter(sweep));
    }

    private static void Validate(decimal demand, IReadOnlyList<AllocationCandidate> candidates, decimal costWeight)
    {
        if (demand <= 0m) throw new InvalidOptimizationRequestException("Demand must be greater than zero.");
        if (candidates.Count is < 1 or > MaxCandidates)
            throw new InvalidOptimizationRequestException($"Between 1 and {MaxCandidates} candidate sources are required.");
        if (costWeight is < 0m or > 1m) throw new InvalidOptimizationRequestException("Cost weight must be between 0 and 1.");
        if (candidates.Select(c => c.Id).Distinct().Count() != candidates.Count)
            throw new InvalidOptimizationRequestException("Candidate identifiers must be unique.");

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c.Name)) throw new InvalidOptimizationRequestException("Every candidate needs a name.");
            if (c.Capacity < 0m) throw new InvalidOptimizationRequestException($"{c.Name}: capacity cannot be negative.");
            if (c.UnitCost < 0m) throw new InvalidOptimizationRequestException($"{c.Name}: unit cost cannot be negative.");
            if (c.UnitRisk is < 0m or > 1m) throw new InvalidOptimizationRequestException($"{c.Name}: unit risk must be between 0 and 1.");
            if (c.MaxShare is <= 0m or > 1m) throw new InvalidOptimizationRequestException($"{c.Name}: max share must be above 0 and at most 1.");
        }

        var deliverable = candidates.Sum(c => Math.Min(c.Capacity, c.MaxShare * demand));
        if (deliverable < demand)
        {
            throw new InfeasibleAllocationException(
                $"Demand {demand:N2} exceeds what the candidates can supply under their capacity and share caps ({deliverable:N2}).");
        }
    }

    private static AllocationPlan Solve(decimal demand, IReadOnlyList<AllocationCandidate> candidates, decimal costWeight)
    {
        var riskWeight = 1m - costWeight;
        var minCost = candidates.Min(c => c.UnitCost);
        var maxCost = candidates.Max(c => c.UnitCost);
        var minRisk = candidates.Min(c => c.UnitRisk);
        var maxRisk = candidates.Max(c => c.UnitRisk);

        decimal Normalize(decimal value, decimal min, decimal max) => max == min ? 0m : (value - min) / (max - min);

        var ordered = candidates
            .OrderBy(c => costWeight * Normalize(c.UnitCost, minCost, maxCost) + riskWeight * Normalize(c.UnitRisk, minRisk, maxRisk))
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var remaining = demand;
        var lines = new List<AllocationLine>();
        foreach (var candidate in ordered)
        {
            if (remaining <= 0m) break;
            var units = Math.Min(Math.Min(candidate.Capacity, candidate.MaxShare * demand), remaining);
            if (units <= 0m) continue;

            remaining -= units;
            lines.Add(new AllocationLine(
                candidate.Id,
                candidate.Name,
                Math.Round(units, UnitDecimals),
                Math.Round(units * candidate.UnitCost, CurrencyDecimals),
                Math.Round(units * candidate.UnitRisk, UnitDecimals)));
        }

        return new AllocationPlan(
            costWeight,
            riskWeight,
            lines,
            lines.Sum(l => l.Units),
            lines.Sum(l => l.Cost),
            lines.Sum(l => l.RiskExposure));
    }

    /// <summary>Keeps distinct plans that are not dominated on (total cost, total risk), sorted by cost.</summary>
    private static IReadOnlyList<AllocationPlan> ParetoFilter(IReadOnlyList<AllocationPlan> plans)
    {
        var distinct = plans
            .GroupBy(p => string.Join("|", p.Lines.Select(l => $"{l.CandidateId}:{l.Units}")))
            .Select(g => g.First())
            .ToList();

        var frontier = distinct.Where(p => !distinct.Any(other =>
                other.TotalCost <= p.TotalCost && other.TotalRiskExposure <= p.TotalRiskExposure &&
                (other.TotalCost < p.TotalCost || other.TotalRiskExposure < p.TotalRiskExposure)))
            .OrderBy(p => p.TotalCost)
            .ThenBy(p => p.TotalRiskExposure)
            .ToList();

        return frontier;
    }
}

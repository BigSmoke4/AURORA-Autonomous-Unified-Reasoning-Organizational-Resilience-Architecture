using Aurora.Shared.Kernel;

namespace Aurora.Modules.Workforce.Application;

public enum CoverageStatus
{
    Vacant = 1,
    Shortfall = 2,
    SingleThreaded = 3,
    Covered = 4,
}

public sealed record CoverageResult(int RequiredHeadcount, int ActualHeadcount, decimal CoveragePct, CoverageStatus Status);

/// <summary>
/// Deterministic headcount coverage for a critical role (spec section 19). Coverage is actual divided by
/// required, as a percentage that may exceed 100. A role held by exactly one person is single-threaded.
/// </summary>
public static class WorkforceCoverageCalculator
{
    public static CoverageResult Evaluate(int requiredHeadcount, int actualHeadcount)
    {
        if (requiredHeadcount < 1) throw new ArgumentOutOfRangeException(nameof(requiredHeadcount), requiredHeadcount, "At least one person is required.");
        if (actualHeadcount < 0) throw new ArgumentOutOfRangeException(nameof(actualHeadcount), actualHeadcount, "Headcount must not be negative.");

        var coveragePct = Rounding.Round(actualHeadcount * 100m / requiredHeadcount);
        var status = actualHeadcount switch
        {
            0 => CoverageStatus.Vacant,
            _ when actualHeadcount < requiredHeadcount => CoverageStatus.Shortfall,
            1 => CoverageStatus.SingleThreaded,
            _ => CoverageStatus.Covered,
        };

        return new CoverageResult(requiredHeadcount, actualHeadcount, coveragePct, status);
    }
}

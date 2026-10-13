using Aurora.Modules.Technology.Domain;

namespace Aurora.Modules.Technology.Application;

/// <summary>The advisories that a patch policy is exposed to, measured against its window.</summary>
public sealed record PatchExposure(int AdvisoryCount, decimal? HighestCvss, int OverdueCount, DateTime? EarliestDeadlineUtc);

/// <summary>
/// Links vulnerability advisories to a patch policy. An advisory exposes a policy when it names the same software
/// product and was published after the policy's last patch, because that patch cannot have fixed it. The deadline
/// for each advisory is its publication date plus the policy's patch window. Pure and deterministic, so the same
/// inputs always give the same exposure.
/// </summary>
public static class PatchExposureCalculator
{
    public const decimal CriticalThreshold = 9.0m;
    public const decimal HighThreshold = 7.0m;
    public const decimal MediumThreshold = 4.0m;

    public static PatchExposure Evaluate(PatchPolicy policy, IEnumerable<VulnerabilityAdvisory> advisories, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(policy.SoftwareProduct)) return new PatchExposure(0, null, 0, null);

        var today = DateOnly.FromDateTime(nowUtc);
        var lastPatched = DateOnly.FromDateTime(policy.LastPatchedOnUtc);
        var exposed = advisories
            .Where(a => string.Equals(a.ProductName, policy.SoftwareProduct, StringComparison.OrdinalIgnoreCase))
            .Where(a => DateOnly.FromDateTime(a.PublishedOnUtc) > lastPatched)
            .Select(a => (Advisory: a, Deadline: DateOnly.FromDateTime(a.PublishedOnUtc).AddDays(policy.PatchSlaDays)))
            .ToList();

        if (exposed.Count == 0) return new PatchExposure(0, null, 0, null);

        return new PatchExposure(
            exposed.Count,
            exposed.Max(e => e.Advisory.CvssScore),
            exposed.Count(e => today > e.Deadline),
            exposed.Min(e => e.Deadline).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    /// <summary>The severity band of a CVSS score, using the published CVSS v3 ranges.</summary>
    public static string SeverityOf(decimal cvssScore) => cvssScore switch
    {
        >= CriticalThreshold => "Critical",
        >= HighThreshold => "High",
        >= MediumThreshold => "Medium",
        _ => "Low",
    };
}

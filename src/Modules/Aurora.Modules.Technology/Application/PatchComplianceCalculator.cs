namespace Aurora.Modules.Technology.Application;

public enum PatchStatus
{
    Compliant = 1,
    Overdue = 2,
}

public sealed record PatchComplianceResult(int DaysSincePatch, int DaysOverSla, DateTime DueOnUtc, PatchStatus Status);

/// <summary>
/// Deterministic patch compliance (spec section 22). Age is counted in whole UTC calendar days. An asset is
/// overdue once its age is greater than its service-level window.
/// </summary>
public static class PatchComplianceCalculator
{
    public static PatchComplianceResult Evaluate(DateTime lastPatchedOnUtc, int patchSlaDays, DateTime asOfUtc)
    {
        if (patchSlaDays < 1) throw new ArgumentOutOfRangeException(nameof(patchSlaDays), patchSlaDays, "The service-level window must be at least one day.");

        var lastPatchedDate = lastPatchedOnUtc.Date;
        var asOfDate = asOfUtc.Date;
        if (asOfDate < lastPatchedDate) throw new ArgumentOutOfRangeException(nameof(asOfUtc), asOfUtc, "The evaluation date cannot precede the last patch.");

        var daysSincePatch = (asOfDate - lastPatchedDate).Days;
        var daysOverSla = Math.Max(0, daysSincePatch - patchSlaDays);
        var dueOn = lastPatchedDate.AddDays(patchSlaDays);
        var status = daysOverSla > 0 ? PatchStatus.Overdue : PatchStatus.Compliant;

        return new PatchComplianceResult(daysSincePatch, daysOverSla, dueOn, status);
    }
}

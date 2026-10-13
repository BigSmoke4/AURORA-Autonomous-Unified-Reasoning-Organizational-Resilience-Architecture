using Aurora.Modules.Finance.Application;
using Aurora.Modules.Operations.Application;
using Aurora.Modules.Technology.Application;
using Aurora.Modules.Technology.Domain;
using Aurora.Modules.Workforce.Application;
using Xunit;

namespace Aurora.Tests.Modules;

public class OperationalCalculatorTests
{
    private static readonly DateTime LastPatched = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Workforce_Vacant_WhenNobodyHoldsTheRole()
    {
        var result = WorkforceCoverageCalculator.Evaluate(3, 0);

        Assert.Equal(CoverageStatus.Vacant, result.Status);
        Assert.Equal(0m, result.CoveragePct);
    }

    [Fact]
    public void Workforce_Shortfall_WhenFewerThanRequired()
    {
        var result = WorkforceCoverageCalculator.Evaluate(3, 2);

        Assert.Equal(CoverageStatus.Shortfall, result.Status);
        Assert.Equal(66.67m, result.CoveragePct);
    }

    [Fact]
    public void Workforce_SingleThreaded_WhenExactlyOneHolder()
    {
        Assert.Equal(CoverageStatus.SingleThreaded, WorkforceCoverageCalculator.Evaluate(1, 1).Status);
    }

    [Fact]
    public void Workforce_Covered_WhenAtOrAboveRequiredWithRedundancy()
    {
        var result = WorkforceCoverageCalculator.Evaluate(2, 3);

        Assert.Equal(CoverageStatus.Covered, result.Status);
        Assert.Equal(150m, result.CoveragePct);
    }

    [Fact]
    public void Workforce_RejectsZeroRequirement()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkforceCoverageCalculator.Evaluate(0, 1));
    }

    [Fact]
    public void Budget_WithinBudget_ReportsVarianceAndUtilization()
    {
        var result = BudgetPositionCalculator.Evaluate(100m, 50m);

        Assert.Equal(BudgetStatus.WithinBudget, result.Status);
        Assert.Equal(50m, result.Variance);
        Assert.Equal(50m, result.UtilizationPct);
    }

    [Fact]
    public void Budget_NearLimit_AtNinetyPercentUtilization()
    {
        Assert.Equal(BudgetStatus.NearLimit, BudgetPositionCalculator.Evaluate(100m, 90m).Status);
    }

    [Fact]
    public void Budget_Overspent_WhenSpendExceedsBudget()
    {
        var result = BudgetPositionCalculator.Evaluate(100m, 120m);

        Assert.Equal(BudgetStatus.Overspent, result.Status);
        Assert.Equal(-20m, result.Variance);
    }

    [Fact]
    public void Budget_UtilizationIsNull_WhenBudgetIsZero()
    {
        var result = BudgetPositionCalculator.Evaluate(0m, 0m);

        Assert.Null(result.UtilizationPct);
        Assert.Equal(BudgetStatus.WithinBudget, result.Status);
    }

    [Fact]
    public void Production_OnPlan_AndBelowYieldTarget()
    {
        var result = ProductionPerformanceCalculator.Evaluate(100, 96, 4);

        Assert.Equal(96m, result.AttainmentPct);
        Assert.Equal(AttainmentStatus.OnPlan, result.AttainmentStatus);
        Assert.Equal(95.83m, result.FirstPassYieldPct);
        Assert.Equal(YieldStatus.BelowTarget, result.YieldStatus);
    }

    [Fact]
    public void Production_Behind_WhenBelowAttainmentTarget()
    {
        Assert.Equal(AttainmentStatus.Behind, ProductionPerformanceCalculator.Evaluate(100, 94, 0).AttainmentStatus);
    }

    [Fact]
    public void Production_NoOutput_HasNoYield()
    {
        var result = ProductionPerformanceCalculator.Evaluate(100, 0, 0);

        Assert.Null(result.FirstPassYieldPct);
        Assert.Equal(YieldStatus.NoOutput, result.YieldStatus);
    }

    [Fact]
    public void Production_AtYieldTarget_WithOneDefect()
    {
        Assert.Equal(YieldStatus.AtTarget, ProductionPerformanceCalculator.Evaluate(100, 100, 1).YieldStatus);
    }

    [Fact]
    public void Production_RejectsMoreDefectsThanOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductionPerformanceCalculator.Evaluate(100, 5, 6));
    }

    [Fact]
    public void Patch_Compliant_OnTheDueDate()
    {
        var result = PatchComplianceCalculator.Evaluate(LastPatched, 30, new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(30, result.DaysSincePatch);
        Assert.Equal(0, result.DaysOverSla);
        Assert.Equal(PatchStatus.Compliant, result.Status);
        Assert.Equal(new DateTime(2026, 1, 31), result.DueOnUtc);
    }

    [Fact]
    public void Patch_Overdue_AfterTheWindow()
    {
        var result = PatchComplianceCalculator.Evaluate(LastPatched, 30, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(35, result.DaysSincePatch);
        Assert.Equal(5, result.DaysOverSla);
        Assert.Equal(PatchStatus.Overdue, result.Status);
    }

    [Fact]
    public void Patch_RejectsEvaluationBeforeTheLastPatch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PatchComplianceCalculator.Evaluate(LastPatched, 30, new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void PatchPolicy_RecordPatch_MovesTheLastPatchForward()
    {
        var policy = new PatchPolicy(Guid.NewGuid(), Guid.NewGuid(), LastPatched, 30);
        var later = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        policy.RecordPatch(later);

        Assert.Equal(later, policy.LastPatchedOnUtc);
    }

    [Fact]
    public void PatchPolicy_RecordPatch_RefusesADateBeforeTheRecordedOne()
    {
        var policy = new PatchPolicy(Guid.NewGuid(), Guid.NewGuid(), LastPatched, 30);

        Assert.Throws<InvalidOperationException>(() => policy.RecordPatch(LastPatched.AddDays(-1)));
    }

    [Fact]
    public void PatchPolicy_RejectsWindowOutsideTheAllowedRange()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new PatchPolicy(Guid.NewGuid(), Guid.NewGuid(), LastPatched, PatchPolicy.MaxSlaDays + 1));
    }
}

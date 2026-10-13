using Aurora.Modules.Technology.Application;
using Aurora.Modules.Technology.Domain;
using Xunit;

namespace Aurora.Tests.Technology;

public sealed class PatchExposureCalculatorTests
{
    private const string Product = "Acme ERP Server";
    private const int PatchWindowDays = 30;
    private const int ShortWindowDays = 14;
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PolicyWithoutAProduct_IsNotExposedToAnyAdvisory()
    {
        var policy = new PatchPolicy(OrganizationId, AssetId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), PatchWindowDays);

        var exposure = PatchExposureCalculator.Evaluate(policy, [Advisory(Product, 9.8m, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc))], Now);

        Assert.Equal(new PatchExposure(0, null, 0, null), exposure);
    }

    [Fact]
    public void OnlyAdvisoriesForTheProductPublishedAfterTheLastPatch_CountAsExposure()
    {
        var lastPatched = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var policy = new PatchPolicy(OrganizationId, AssetId, lastPatched, PatchWindowDays, "acme erp server");
        var advisories = new[]
        {
            Advisory("ACME ERP SERVER", 9.8m, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)),
            Advisory(Product, 5.3m, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)),
            Advisory("Plant Historian", 9.9m, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)),
        };

        var exposure = PatchExposureCalculator.Evaluate(policy, advisories, Now);

        Assert.Equal(1, exposure.AdvisoryCount);
        Assert.Equal(5.3m, exposure.HighestCvss);
        Assert.Equal(0, exposure.OverdueCount);
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), exposure.EarliestDeadlineUtc);
    }

    [Fact]
    public void AnAdvisoryPublishedOnTheLastPatchDate_IsNotExposure()
    {
        var lastPatched = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var policy = new PatchPolicy(OrganizationId, AssetId, lastPatched, PatchWindowDays, Product);

        var exposure = PatchExposureCalculator.Evaluate(policy, [Advisory(Product, 7.5m, lastPatched)], Now);

        Assert.Equal(0, exposure.AdvisoryCount);
    }

    [Fact]
    public void AnAdvisoryWhoseWindowHasPassed_IsOverdue()
    {
        var policy = new PatchPolicy(OrganizationId, AssetId, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), ShortWindowDays, Product);
        var advisories = new[]
        {
            Advisory(Product, 9.8m, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)),
            Advisory(Product, 5.3m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
        };

        var exposure = PatchExposureCalculator.Evaluate(policy, advisories, Now);

        Assert.Equal(2, exposure.AdvisoryCount);
        Assert.Equal(1, exposure.OverdueCount);
        Assert.Equal(9.8m, exposure.HighestCvss);
        Assert.Equal(new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc), exposure.EarliestDeadlineUtc);
    }

    [Theory]
    [InlineData("10.0", "Critical")]
    [InlineData("9.0", "Critical")]
    [InlineData("8.9", "High")]
    [InlineData("7.0", "High")]
    [InlineData("6.9", "Medium")]
    [InlineData("4.0", "Medium")]
    [InlineData("3.9", "Low")]
    [InlineData("0.0", "Low")]
    public void SeverityFollowsTheCvssBands(string score, string expected) =>
        Assert.Equal(expected, PatchExposureCalculator.SeverityOf(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void AdvisoryRejectsAScoreOutsideTheCvssRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VulnerabilityAdvisory(OrganizationId, "CVE-2026-2001", Product, 10.1m, Now, "Out of range.", Now));
    }

    [Fact]
    public void RefreshReportsNoChange_WhenTheFeedRepeatsTheStoredValues()
    {
        var advisory = new VulnerabilityAdvisory(OrganizationId, "CVE-2026-2002", Product, 7.5m, Now.Date, "Stored summary.", Now);

        var changed = advisory.Refresh(Product, 7.5m, Now.Date, "Stored summary.", Now.AddDays(1));

        Assert.False(changed);
    }

    private static VulnerabilityAdvisory Advisory(string product, decimal cvss, DateTime publishedUtc) =>
        new(OrganizationId, $"CVE-2026-{Guid.NewGuid().ToString("N")[..4]}", product, cvss, publishedUtc, "Test advisory.", Now);
}

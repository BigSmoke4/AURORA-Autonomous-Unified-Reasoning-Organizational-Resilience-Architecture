using Aurora.Modules.Alerts.Application;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Monitoring.Application;
using Aurora.Modules.Monitoring.Domain;
using Xunit;

namespace Aurora.Tests.Modules;

public class MonitoringAlertTests
{
    private static readonly DateTime RaisedAt = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(10, ThresholdState.Normal)]
    [InlineData(20, ThresholdState.Normal)]
    [InlineData(9.99, ThresholdState.BelowMinimum)]
    [InlineData(20.01, ThresholdState.AboveMaximum)]
    public void Threshold_BoundsAreInclusive(double value, ThresholdState expected)
    {
        Assert.Equal(expected, MetricThresholdEvaluator.Evaluate((decimal)value, 10m, 20m));
    }

    [Fact]
    public void Threshold_WithOnlyMaximum_IgnoresTheMissingMinimum()
    {
        Assert.Equal(ThresholdState.Normal, MetricThresholdEvaluator.Evaluate(-5m, null, 20m));
    }

    [Fact]
    public void Definition_RequiresAtLeastOneThreshold()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MetricDefinition(Guid.NewGuid(), "Temp", "C", null, null));
    }

    [Fact]
    public void Definition_RejectsMinimumNotBelowMaximum()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MetricDefinition(Guid.NewGuid(), "Temp", "C", 30m, 20m));
    }

    [Fact]
    public void Alert_StartsOpen_AndCanBeAcknowledged()
    {
        var alert = new Alert(Guid.NewGuid(), "monitoring:metric:x", "Above maximum", null, AlertSeverity.Warning, RaisedAt);
        var userId = Guid.NewGuid();

        alert.Acknowledge(userId, RaisedAt.AddMinutes(5));

        Assert.Equal(AlertStatus.Acknowledged, alert.Status);
        Assert.Equal(userId, alert.AcknowledgedByUserId);
        Assert.Equal(RaisedAt.AddMinutes(5), alert.AcknowledgedAtUtc);
    }

    [Fact]
    public void Alert_AcknowledgeRefusedWhenNotOpen()
    {
        var alert = new Alert(Guid.NewGuid(), "monitoring:metric:x", "Above maximum", null, AlertSeverity.Warning, RaisedAt);
        alert.Acknowledge(Guid.NewGuid(), RaisedAt);

        Assert.Throws<InvalidOperationException>(() => alert.Acknowledge(Guid.NewGuid(), RaisedAt));
    }

    [Fact]
    public void Alert_ResolveTwice_IsRefused()
    {
        var alert = new Alert(Guid.NewGuid(), "monitoring:metric:x", "Above maximum", null, AlertSeverity.Warning, RaisedAt);
        alert.Resolve(RaisedAt.AddHours(1));

        Assert.Equal(AlertStatus.Resolved, alert.Status);
        Assert.Throws<InvalidOperationException>(() => alert.Resolve(RaisedAt.AddHours(2)));
    }

    [Fact]
    public void Alert_RejectsEmptyUserOnAcknowledge()
    {
        var alert = new Alert(Guid.NewGuid(), "monitoring:metric:x", "Above maximum", null, AlertSeverity.Warning, RaisedAt);

        Assert.ThrowsAny<ArgumentException>(() => alert.Acknowledge(Guid.Empty, RaisedAt));
    }

    [Fact]
    public void SourceKey_IsStableForAMetric()
    {
        var metricId = new Guid("3f2b1c4d-0000-4000-8000-000000000001");

        Assert.Equal("monitoring:metric:3f2b1c4d-0000-4000-8000-000000000001", AlertSourceKeys.ForMetric(metricId));
    }
}

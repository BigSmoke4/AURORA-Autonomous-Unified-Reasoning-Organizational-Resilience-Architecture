using Aurora.Web.Integration.Feeds;
using Xunit;

namespace Aurora.Tests.Feeds;

/// <summary>Parses the recorded fixture that the fixture feed adapter serves, and rejects malformed payloads.</summary>
public sealed class MetricFeedParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private static string FixtureText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Web", "Aurora.Web", "Data", "Feeds", "Fixtures",
                FixtureMetricFeedAdapter.FixtureFileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException("The metric feed fixture was not found above the test output folder.");
    }

    [Fact]
    public void FixtureRowsAreAcceptedOrRejectedRowByRow()
    {
        var result = MetricFeedParser.Parse(FixtureText(), Today);

        Assert.Equal(4, result.Accepted.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Accepted.Select(r => r.Index).ToArray());
        var rejected = Assert.Single(result.Rejected);
        Assert.StartsWith("Row 5:", rejected, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservedDatesAreStoredAsUtcMidnight()
    {
        var first = MetricFeedParser.Parse(FixtureText(), Today).Accepted[0];

        Assert.Equal(DateTimeKind.Utc, first.ObservedOnUtc.Kind);
        Assert.Equal(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc), first.ObservedOnUtc);
        Assert.Equal(14.2m, first.Value);
    }

    [Fact]
    public void ReadingsAfterTheIngestionDateAreRejected()
    {
        var result = MetricFeedParser.Parse(FixtureText(), new DateOnly(2026, 10, 10));

        Assert.Contains(result.Rejected, reason => reason.StartsWith("Row 3:", StringComparison.Ordinal)
            && reason.Contains("after the ingestion date", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedSchemaVersionFailsTheWholePayload()
    {
        Assert.Throws<FormatException>(() => MetricFeedParser.Parse("{\"schemaVersion\":2,\"readings\":[]}", Today));
    }

    [Fact]
    public void MalformedJsonFailsTheWholePayload()
    {
        Assert.Throws<FormatException>(() => MetricFeedParser.Parse("{\"schemaVersion\":1,", Today));
    }

    [Fact]
    public void MissingReadingsArrayFailsTheWholePayload()
    {
        Assert.Throws<FormatException>(() => MetricFeedParser.Parse("{\"schemaVersion\":1}", Today));
    }

    [Fact]
    public void PayloadOverTheRowLimitFailsBeforeAnyRowIsRead()
    {
        var rows = string.Join(',', Enumerable.Repeat("{\"metric\":\"x\",\"observedOn\":\"2026-10-10\",\"value\":1}", MetricFeedParser.MaxRows + 1));

        Assert.Throws<FormatException>(() => MetricFeedParser.Parse($"{{\"schemaVersion\":1,\"readings\":[{rows}]}}", Today));
    }
}

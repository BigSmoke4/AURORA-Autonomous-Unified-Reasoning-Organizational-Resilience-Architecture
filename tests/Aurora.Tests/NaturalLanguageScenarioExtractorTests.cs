using Aurora.Modules.Scenarios.Application;
using Aurora.Modules.Scenarios.Domain;
using Xunit;

namespace Aurora.Tests;

public class NaturalLanguageScenarioExtractorTests
{
    [Fact]
    public void Extract_ParsesPercentageDurationAndKnownTarget()
    {
        var supplierId = Guid.NewGuid();
        var candidates = new Dictionary<string, Guid> { ["Northbridge Components"] = supplierId };
        var extractor = new NaturalLanguageScenarioExtractor();

        var draft = extractor.Extract(
            "What happens if Northbridge Components loses 60% capacity for 90 days?", candidates);

        Assert.Equal(supplierId, draft.TargetNodeId);
        Assert.Equal(ScenarioEventType.CapacityReduction, draft.EventType);
        Assert.Equal(0.60m, draft.Severity);
        Assert.Equal(90, draft.DurationDays);
        Assert.Empty(draft.MissingFields);
    }

    [Fact]
    public void Extract_FlagsMissingFields_InsteadOfGuessing()
    {
        var extractor = new NaturalLanguageScenarioExtractor();
        var draft = extractor.Extract("Something bad might happen soon.", new Dictionary<string, Guid>());

        Assert.Null(draft.TargetNodeId);
        Assert.NotEmpty(draft.MissingFields);
    }

    [Fact]
    public void Extract_ParsesWordNumberMonths()
    {
        var candidates = new Dictionary<string, Guid> { ["Factory Alpha"] = Guid.NewGuid() };
        var extractor = new NaturalLanguageScenarioExtractor();

        var draft = extractor.Extract("Factory Alpha shuts down for three months.", candidates);

        Assert.Equal(ScenarioEventType.FullShutdown, draft.EventType);
        Assert.Equal(90, draft.DurationDays);
        Assert.Equal(1.0m, draft.Severity); // shutdown implies full severity
    }
}

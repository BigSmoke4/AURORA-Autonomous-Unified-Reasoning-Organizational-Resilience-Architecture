using System.Text.RegularExpressions;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Modules.Scenarios.Application;

public sealed record ScenarioDraft(
    string? TargetNodeName,
    Guid? TargetNodeId,
    ScenarioEventType? EventType,
    decimal? Severity,
    int? DurationDays,
    IReadOnlyList<string> MissingFields);

/// <summary>
/// Spec section 16 ("natural language scenario creation") implemented honestly: this is a
/// deterministic, rule-based pattern extractor over plain English phrasing like "our largest
/// supplier loses 60% capacity for three months" — NOT a call to a large language model.
/// AURORA's design principle (spec section 100/101) is that the LLM must never be the
/// authoritative source of structured data feeding the simulation engine; this extractor
/// enforces that even when a real LLM is wired in later via IAiProvider (see Modules.AI),
/// because whatever the LLM proposes should still be validated against the graph and echoed
/// back to the user as a ScenarioDraft with any MissingFields called out, never silently
/// invented. Swap or augment this class with an IAiProvider-backed extractor without changing
/// the contract once a real model is configured.
/// </summary>
public sealed class NaturalLanguageScenarioExtractor
{
    private static readonly Dictionary<string, ScenarioEventType> EventKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shutdown"] = ScenarioEventType.FullShutdown,
        ["shuts down"] = ScenarioEventType.FullShutdown,
        ["closes"] = ScenarioEventType.FullShutdown,
        ["fails"] = ScenarioEventType.FullShutdown,
        ["failure"] = ScenarioEventType.FullShutdown,
        ["loses"] = ScenarioEventType.CapacityReduction,
        ["reduction"] = ScenarioEventType.CapacityReduction,
        ["increase in demand"] = ScenarioEventType.DemandIncrease,
        ["demand increase"] = ScenarioEventType.DemandIncrease,
        ["demand collapse"] = ScenarioEventType.DemandDecrease,
        ["demand drop"] = ScenarioEventType.DemandDecrease,
        ["cost increase"] = ScenarioEventType.CostIncrease,
        ["price increase"] = ScenarioEventType.CostIncrease,
    };

    private static readonly Regex PercentPattern = new(@"(\d{1,3})\s*%", RegexOptions.Compiled);
    private static readonly Regex DaysPattern = new(@"(\d{1,3})\s*day", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WeeksPattern = new(@"(\d{1,3})\s*week", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MonthsPattern = new(@"(\d{1,3})\s*month", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WordMonthsPattern = new(@"\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+months?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, int> WordNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
        ["eleven"] = 11, ["twelve"] = 12
    };

    /// <param name="text">The user's free-text request.</param>
    /// <param name="candidateNodes">Display names of graph nodes eligible as a target, so the
    /// extractor only ever matches a real entity — it never fabricates a target.</param>
    public ScenarioDraft Extract(string text, IReadOnlyDictionary<string, Guid> candidateNodes)
    {
        var missing = new List<string>();

        // Target: match the longest candidate node name that appears in the text.
        Guid? targetId = null;
        string? targetName = null;
        foreach (var (name, id) in candidateNodes.OrderByDescending(kv => kv.Key.Length))
        {
            if (text.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                targetId = id;
                targetName = name;
                break;
            }
        }
        if (targetId is null) missing.Add("Target entity (no known supplier, factory, or asset name was recognized in the text)");

        // Event type
        ScenarioEventType? eventType = null;
        foreach (var (keyword, type) in EventKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                eventType = type;
                break;
            }
        }
        if (eventType is null) missing.Add("Event type (e.g. capacity reduction, shutdown, demand increase)");

        // Severity
        decimal? severity = null;
        var percentMatch = PercentPattern.Match(text);
        if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var pct))
        {
            severity = Math.Clamp(pct / 100m, 0m, 1m);
        }
        else if (eventType == ScenarioEventType.FullShutdown)
        {
            severity = 1.0m;
        }
        else
        {
            missing.Add("Severity (what percentage is affected?)");
        }

        // Duration
        int? durationDays = null;
        if (DaysPattern.Match(text) is { Success: true } dm) durationDays = int.Parse(dm.Groups[1].Value);
        else if (WeeksPattern.Match(text) is { Success: true } wm) durationDays = int.Parse(wm.Groups[1].Value) * 7;
        else if (MonthsPattern.Match(text) is { Success: true } mm) durationDays = int.Parse(mm.Groups[1].Value) * 30;
        else if (WordMonthsPattern.Match(text) is { Success: true } wmm && WordNumbers.TryGetValue(wmm.Groups[1].Value, out var n))
            durationDays = n * 30;

        if (durationDays is null) missing.Add("Duration (how long does the event last?)");

        return new ScenarioDraft(targetName, targetId, eventType, severity, durationDays, missing);
    }
}

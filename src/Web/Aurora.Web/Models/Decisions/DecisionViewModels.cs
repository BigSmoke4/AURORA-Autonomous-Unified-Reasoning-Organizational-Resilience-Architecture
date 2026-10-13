using Aurora.Modules.AI.Domain;
using Aurora.Modules.Decisions.Domain;

namespace Aurora.Web.Models.Decisions;

public sealed class DecisionQueueViewModel
{
    public IReadOnlyList<Decision> Decisions { get; init; } = Array.Empty<Decision>();
}

public sealed class DecisionDetailsViewModel
{
    public required Decision Decision { get; init; }
    public IReadOnlyList<KeyValuePair<string, string>> Factors { get; init; } = Array.Empty<KeyValuePair<string, string>>();
    public bool CanDecide { get; init; }
    public bool CanStartExecution { get; init; }
    public bool CanRecordOutcome { get; init; }
    public bool CanDebate { get; init; }
    public DebateSession? Debate { get; init; }
    public IReadOnlyList<DebateArgument> DebateArguments { get; init; } = Array.Empty<DebateArgument>();
}

public sealed class RecordOutcomeForm
{
    public const int MaxSummaryLength = 2000;
    public decimal ActualRevenueImpact { get; set; }
    public string ActualOutcomeSummary { get; set; } = string.Empty;
}

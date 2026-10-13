using System.ComponentModel.DataAnnotations;
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
}

public sealed class RecordOutcomeForm
{
    public const int MaxSummaryLength = 2000;

    [Required]
    [Range(typeof(decimal), "-1000000000000", "1000000000000", ErrorMessage = "Enter a realistic revenue impact.")]
    public decimal ActualRevenueImpact { get; set; }

    [Required]
    [StringLength(MaxSummaryLength, MinimumLength = 3)]
    public string ActualOutcomeSummary { get; set; } = string.Empty;
}

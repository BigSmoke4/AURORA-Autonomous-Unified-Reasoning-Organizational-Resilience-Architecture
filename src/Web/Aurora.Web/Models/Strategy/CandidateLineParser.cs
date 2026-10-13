using System.Globalization;
using Aurora.Modules.Optimization.Domain;

namespace Aurora.Web.Models.Strategy;

/// <summary>
/// Parses one candidate per line: Name | capacity | unit cost | unit risk (0-1) | max share (0-1].
/// Parsing is culture-invariant, so a value means the same number on every server.
/// </summary>
public static class CandidateLineParser
{
    private const int FieldCount = 5;

    public static (IReadOnlyList<AllocationCandidate> Candidates, IReadOnlyList<string> Errors) Parse(string text)
    {
        var candidates = new List<AllocationCandidate>();
        var errors = new List<string>();
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length is 0 or > OptimizationForm.MaxCandidateLines)
        {
            errors.Add($"Enter between 1 and {OptimizationForm.MaxCandidateLines} candidate lines.");
            return (candidates, errors);
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var fields = lines[i].Split('|', StringSplitOptions.TrimEntries);
            if (fields.Length != FieldCount)
            {
                errors.Add($"Line {i + 1}: expected 5 fields separated by '|'.");
                continue;
            }

            if (!TryDecimal(fields[1], out var capacity) ||
                !TryDecimal(fields[2], out var unitCost) ||
                !TryDecimal(fields[3], out var unitRisk) ||
                !TryDecimal(fields[4], out var maxShare))
            {
                errors.Add($"Line {i + 1}: capacity, unit cost, unit risk and max share must be numbers.");
                continue;
            }

            candidates.Add(new AllocationCandidate($"S{i + 1}", fields[0], capacity, unitCost, unitRisk, maxShare));
        }

        return (candidates, errors);
    }

    private static bool TryDecimal(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}

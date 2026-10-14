namespace Aurora.Shared.Kernel;

/// <summary>
/// Constructor-level invariant checks shared by every module entity. An entity never persists in an
/// invalid state: a violated invariant throws ArgumentException before any row is staged.
/// </summary>
public static class DomainGuard
{
    public static string RequireText(string? value, int maxLength, string paramName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new ArgumentException("A value is required.", paramName);
        if (trimmed.Length > maxLength) throw new ArgumentException($"The value must be at most {maxLength} characters.", paramName);
        return trimmed;
    }

    public static string? OptionalText(string? value, int maxLength, string paramName)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is not null && trimmed.Length > maxLength)
            throw new ArgumentException($"The value must be at most {maxLength} characters.", paramName);
        return trimmed;
    }

    public static Guid RequireId(Guid value, string paramName) =>
        value == Guid.Empty ? throw new ArgumentException("An identifier is required.", paramName) : value;

    public static int RequireRange(int value, int min, int max, string paramName) =>
        value < min || value > max
            ? throw new ArgumentOutOfRangeException(paramName, value, $"The value must be between {min} and {max}.")
            : value;

    public static decimal RequireNonNegative(decimal value, string paramName) =>
        value < 0m ? throw new ArgumentOutOfRangeException(paramName, value, "The value must not be negative.") : value;

    public static decimal RequirePositive(decimal value, string paramName) =>
        value <= 0m ? throw new ArgumentOutOfRangeException(paramName, value, "The value must be greater than zero.") : value;

    /// <summary>
    /// Normalizes a timestamp to UTC. Values from forms arrive as Unspecified calendar dates and are
    /// interpreted as UTC, because every stored instant in AURORA is UTC (spec section 10).
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

using System.Globalization;
using System.Text;

namespace Aurora.Web.Models.Modules;

/// <summary>Display helpers, status tones and fixed input limits shared by the Phase 3 module pages.</summary>
public static class ModuleFormatting
{
    public const string ToneHealthy = "healthy";
    public const string ToneWarning = "warning";
    public const string ToneCritical = "critical";
    public const string NotApplicable = "n/a";

    public const decimal MinimumMoney = 0m;
    public const decimal MaximumMoney = 999999999999.99m;
    public const decimal MinimumPositiveMoney = 0.01m;
    public const decimal MinimumIndex = 0.0001m;
    public const decimal MaximumIndex = 1000000m;
    public const decimal MinimumReadingValue = -999999999999.9999m;
    public const decimal MaximumReadingValue = 999999999999.9999m;
    public const decimal MaximumDemand = 1000000000m;
    public const int MaximumDelayDays = 365;

    private const string NumberFormat = "0.00";
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Turns an enum name such as SingleThreaded into the readable label "Single threaded".</summary>
    public static string Humanize(string pascalCase)
    {
        var builder = new StringBuilder(pascalCase.Length + 4);
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (i > 0 && char.IsUpper(c)) builder.Append(' ').Append(char.ToLowerInvariant(c));
            else builder.Append(c);
        }

        return builder.ToString();
    }

    public static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    public static string Number(decimal value) => value.ToString(NumberFormat, CultureInfo.InvariantCulture);

    public static string Percent(decimal? value) =>
        value is { } v ? v.ToString(NumberFormat, CultureInfo.InvariantCulture) + "%" : NotApplicable;

    public static string Date(DateTime value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);
}

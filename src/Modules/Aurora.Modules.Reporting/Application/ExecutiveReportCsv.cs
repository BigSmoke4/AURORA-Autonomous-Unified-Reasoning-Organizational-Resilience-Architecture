using System.Text;

namespace Aurora.Modules.Reporting.Application;

public sealed record ReportLine(string Area, string Metric, string Value, string Status);

/// <summary>
/// Renders the executive report as RFC 4180 CSV. Fields are quoted when needed, and any field that a
/// spreadsheet would read as a formula is prefixed with an apostrophe, so an exported value cannot run code.
/// </summary>
public static class ExecutiveReportCsv
{
    public const string Header = "Area,Metric,Value,Status";
    public const string FormulaGuard = "'";
    private const string LineBreak = "\r\n";
    private const string Quote = "\"";
    private const string EscapedQuote = "\"\"";
    private const char Separator = ',';
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] QuotingTriggers = [Separator, '"', '\r', '\n'];

    public static string Build(IReadOnlyList<ReportLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var builder = new StringBuilder();
        builder.Append(Header).Append(LineBreak);
        foreach (var line in lines)
        {
            builder.Append(Escape(line.Area)).Append(Separator)
                .Append(Escape(line.Metric)).Append(Separator)
                .Append(Escape(line.Value)).Append(Separator)
                .Append(Escape(line.Status)).Append(LineBreak);
        }

        return builder.ToString();
    }

    public static string Escape(string field)
    {
        var value = field.Length > 0 && FormulaTriggers.Contains(field[0]) ? FormulaGuard + field : field;
        var needsQuotes = value.IndexOfAny(QuotingTriggers) >= 0;
        return needsQuotes ? Quote + value.Replace(Quote, EscapedQuote, StringComparison.Ordinal) + Quote : value;
    }
}

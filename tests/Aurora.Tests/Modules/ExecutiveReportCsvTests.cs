using Aurora.Modules.Reporting.Application;
using Aurora.Web.Models.Modules;
using Xunit;

namespace Aurora.Tests.Modules;

public class ExecutiveReportCsvTests
{
    private const string Header = "Area,Metric,Value,Status";
    private const string LineBreak = "\r\n";

    [Fact]
    public void Build_WritesHeaderOnly_WhenThereAreNoLines()
    {
        Assert.Equal(Header + LineBreak, ExecutiveReportCsv.Build(Array.Empty<ReportLine>()));
    }

    [Fact]
    public void Build_UsesCrlfAndOneRowPerLine()
    {
        var csv = ExecutiveReportCsv.Build(new[] { new ReportLine("Finance", "PRD-100", "1.00", "OK") });

        Assert.Equal(Header + LineBreak + "Finance,PRD-100,1.00,OK" + LineBreak, csv);
    }

    [Fact]
    public void Escape_QuotesFieldsContainingSeparatorsAndDoublesQuotes()
    {
        Assert.Equal("\"a, \"\"b\"\"\"", ExecutiveReportCsv.Escape("a, \"b\""));
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-5.00", "'-5.00")]
    [InlineData("@cmd", "'@cmd")]
    public void Escape_PrefixesFormulaTriggers(string field, string expected)
    {
        Assert.Equal(expected, ExecutiveReportCsv.Escape(field));
    }

    [Fact]
    public void Escape_LeavesPlainFieldsUnchanged()
    {
        Assert.Equal("Plain value", ExecutiveReportCsv.Escape("Plain value"));
    }

    [Theory]
    [InlineData("Unconcentrated", "Unconcentrated")]
    [InlineData("SingleThreaded", "Single threaded")]
    [InlineData("BelowReorderPoint", "Below reorder point")]
    public void Humanize_SplitsPascalCaseIntoReadableLabels(string input, string expected)
    {
        Assert.Equal(expected, ModuleFormatting.Humanize(input));
    }

    [Fact]
    public void Percent_ShowsNotApplicable_WhenMissing()
    {
        Assert.Equal(ModuleFormatting.NotApplicable, ModuleFormatting.Percent(null));
        Assert.Equal("12.50%", ModuleFormatting.Percent(12.5m));
    }
}

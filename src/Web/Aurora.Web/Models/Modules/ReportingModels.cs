using Aurora.Modules.Reporting.Application;

namespace Aurora.Web.Models.Modules;

public sealed record ReportingIndexViewModel(IReadOnlyList<ReportLine> Lines, IReadOnlyList<ExecutiveReportRunRow> Runs);

public sealed record ExecutiveReportRunRow(DateTime GeneratedAtUtc, string RequestedByName, int LineCount, string ContentSha256);

using Aurora.Modules.Reporting.Application;

namespace Aurora.Web.Models.Modules;

public sealed record ReportingIndexViewModel(IReadOnlyList<ReportLine> Lines);

using System.Text;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Reporting.Application;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>The executive report across all modules, shown on screen and exported as CSV (spec section 30).</summary>
[Authorize(Policy = AuroraPermissions.ReportsRead)]
public class ReportingController : ModuleControllerBase
{
    private const string CsvContentType = "text/csv";
    private const string ExportFileNamePrefix = "aurora-executive-report-";
    private const string ExportFileNameDateFormat = "yyyyMMdd";
    private const string ExportFileNameExtension = ".csv";

    private readonly ExecutiveReportBuilder _reports;

    public ReportingController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit,
        TimeProvider clock, ExecutiveReportBuilder reports)
        : base(organizations, db, audit, clock)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return View("NoOrganization");

        var lines = await _reports.BuildAsync(ct);
        return View(new ReportingIndexViewModel(lines));
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var lines = await _reports.BuildAsync(ct);
        RecordAudit(org, AuditActions.ReportExported, nameof(Organization), org.Id.ToString(), $"{lines.Count} lines");
        await Db.SaveChangesAsync(ct);

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(ExecutiveReportCsv.Build(lines));
        var fileName = $"{ExportFileNamePrefix}{TodayUtc().ToString(ExportFileNameDateFormat, System.Globalization.CultureInfo.InvariantCulture)}{ExportFileNameExtension}";
        return File(bytes, CsvContentType, fileName);
    }
}

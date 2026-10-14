using System.Security.Cryptography;
using System.Text;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Reporting.Application;
using Aurora.Modules.Reporting.Domain;
using Aurora.Web.Data;
using Aurora.Web.Data.Repositories;
using Aurora.Web.Models;
using Aurora.Web.Models.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>The executive report across all modules, shown on screen and exported as CSV (spec section 30).</summary>
[Authorize(Policy = AuroraPermissions.ReportsRead)]
public class ReportingController : ModuleControllerBase
{
    private const int RecentRunLimit = 10;
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
        var runs = await Entities<ExecutiveReportRun>().ListAsync(null, q => q.OrderByDescending(r => r.GeneratedAtUtc), ct, RecentRunLimit);
        var runRows = runs.Select(r => new ExecutiveReportRunRow(r.GeneratedAtUtc, r.RequestedByName, r.LineCount, r.ContentSha256)).ToList();
        return View(new ReportingIndexViewModel(lines, runRows));
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var lines = await _reports.BuildAsync(ct);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(ExecutiveReportCsv.Build(lines));
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        var run = new ExecutiveReportRun(org.Id, TodayUtc(), CurrentUserId(), User.ActorName(), lines.Count, hash);
        Entities<ExecutiveReportRun>().Add(run);
        RecordAudit(org, AuditActions.ReportExported, nameof(ExecutiveReportRun), run.Id.ToString(), $"{lines.Count} lines, sha256 {hash}");
        await Db.SaveChangesAsync(ct);

        var fileName = $"{ExportFileNamePrefix}{TodayUtc().ToString(ExportFileNameDateFormat, System.Globalization.CultureInfo.InvariantCulture)}{ExportFileNameExtension}";
        return File(bytes, CsvContentType, fileName);
    }
}

using Aurora.Modules.Audit.Contracts;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Models;
using Aurora.Web.Models.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize(Policy = AuroraPermissions.AuditRead)]
public class AuditController : Controller
{
    private const int RecentEntryLimit = 200;

    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IAuditRepository _audit;

    public AuditController(CurrentOrganizationAccessor orgAccessor, IAuditRepository audit)
    {
        _orgAccessor = orgAccessor;
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        var entries = await _audit.GetRecentAsync(org.Id, RecentEntryLimit, ct);
        return View(new AuditIndexViewModel { Entries = entries });
    }
}

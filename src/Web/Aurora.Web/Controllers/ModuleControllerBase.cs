using System.Security.Claims;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>
/// Shared plumbing for the Phase 3 module controllers: tenant resolution, the signed-in user, audit staging and
/// one-off messages. Every write is saved with SaveChangesAsync, which commits the audit row in the same transaction.
/// </summary>
public abstract class ModuleControllerBase : Controller
{
    protected ModuleControllerBase(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
    {
        Organizations = organizations;
        Db = db;
        Audit = audit;
        Clock = clock;
    }

    protected CurrentOrganizationAccessor Organizations { get; }

    protected AuroraDbContext Db { get; }

    protected AuditRecorder Audit { get; }

    protected TimeProvider Clock { get; }

    protected Task<Organization?> CurrentOrganizationAsync(CancellationToken ct) => Organizations.GetCurrentAsync(ct);

    protected Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    protected void RecordAudit(Organization org, string action, string entityType, string entityId, string? details = null) =>
        Audit.Record(org.Id, CurrentUserId(), User.ActorName(), action, entityType, entityId, AuditOutcome.Success, details);

    protected void Success(string message) => TempData[FlashKeys.Success] = message;

    protected void Failure(string message) => TempData[FlashKeys.Error] = message;

    protected DateTime TodayUtc() => Clock.GetUtcNow().UtcDateTime;
}

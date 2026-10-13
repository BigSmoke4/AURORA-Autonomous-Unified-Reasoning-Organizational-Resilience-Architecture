using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using System.Diagnostics;
using Aurora.Modules.AI.Contracts;
using Aurora.Shared.Observability;
using Aurora.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

namespace Aurora.Web.Controllers;

/// <summary>The AI Command Panel backend (spec section 59): "What would be affected if X
/// failed?" runs a real InvestigationOrchestrator pipeline against the graph and returns
/// evidence-backed results, not a chat completion.</summary>
[Authorize(Policy = AuroraPermissions.AiInvestigate)]
[EnableRateLimiting(RateLimitPolicies.AiInvestigation)]
[Route("api/ai")]
[ApiController]
public class AiController : ControllerBase
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly InvestigationOrchestrator _orchestrator;
    private readonly IAiInvestigationRepository _repository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditRecorder _audit;

    public AiController(
        CurrentOrganizationAccessor orgAccessor,
        InvestigationOrchestrator orchestrator,
        IAiInvestigationRepository repository,
        UserManager<ApplicationUser> userManager,
        AuditRecorder audit)
    {
        _audit = audit;
        _orgAccessor = orgAccessor;
        _orchestrator = orchestrator;
        _repository = repository;
        _userManager = userManager;
    }

    public record InvestigateRequest(Guid TargetNodeId, string Question);

    [HttpPost("investigate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Investigate([FromBody] InvestigateRequest request, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var investigationTimer = Stopwatch.StartNew();
        var investigation = await _orchestrator.InvestigateNodeRiskAsync(org.Id, userId, request.TargetNodeId, request.Question, ct);
        AuroraTelemetry.InvestigationDurationMs.Record(investigationTimer.Elapsed.TotalMilliseconds);

        await _repository.AddAsync(investigation, ct);
        _audit.Record(org.Id, userId, User.ActorName(), AuditActions.AiInvestigation, nameof(AiInvestigation),
            investigation.Id.ToString(), investigation.Status == InvestigationStatus.Completed ? AuditOutcome.Success : AuditOutcome.Failure,
            $"Target {request.TargetNodeId}; narrative source {investigation.NarrativeSource}; model {investigation.ModelVersion}.");
        await _repository.SaveChangesAsync(ct);

        return Ok(new
        {
            id = investigation.Id,
            question = investigation.Question,
            status = investigation.Status.ToString(),
            narrative = investigation.NarrativeExplanation,
            narrativeSource = investigation.NarrativeSource.ToString(),
            modelVersion = investigation.ModelVersion,
            confidence = investigation.Confidence,
            evidence = investigation.Evidence.Select(e => new
            {
                tool = e.ToolName,
                summary = e.Summary,
                durationMs = e.Duration.TotalMilliseconds
            })
        });
    }

    [HttpGet("investigations/recent")]
    public async Task<IActionResult> Recent(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var recent = await _repository.GetByOrganizationAsync(org.Id, 10, ct);
        return Ok(recent.Select(i => new { i.Id, i.Question, i.Confidence, Status = i.Status.ToString(), i.StartedAtUtc }));
    }
}

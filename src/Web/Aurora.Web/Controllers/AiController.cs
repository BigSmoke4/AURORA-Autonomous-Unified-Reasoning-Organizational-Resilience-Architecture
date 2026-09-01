using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Contracts;
using Aurora.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Aurora.Modules.Identity.Domain;

namespace Aurora.Web.Controllers;

/// <summary>The AI Command Panel backend (spec section 59): "What would be affected if X
/// failed?" runs a real InvestigationOrchestrator pipeline against the graph and returns
/// evidence-backed results, not a chat completion.</summary>
[Authorize(Policy = "AI.Investigate")]
[Route("api/ai")]
[ApiController]
public class AiController : ControllerBase
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly InvestigationOrchestrator _orchestrator;
    private readonly IAiInvestigationRepository _repository;
    private readonly UserManager<ApplicationUser> _userManager;

    public AiController(
        CurrentOrganizationAccessor orgAccessor,
        InvestigationOrchestrator orchestrator,
        IAiInvestigationRepository repository,
        UserManager<ApplicationUser> userManager)
    {
        _orgAccessor = orgAccessor;
        _orchestrator = orchestrator;
        _repository = repository;
        _userManager = userManager;
    }

    public record InvestigateRequest(Guid TargetNodeId, string Question);

    [HttpPost("investigate")]
    public async Task<IActionResult> Investigate([FromBody] InvestigateRequest request, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var investigation = await _orchestrator.InvestigateNodeRiskAsync(org.Id, userId, request.TargetNodeId, request.Question, ct);

        await _repository.AddAsync(investigation, ct);
        await _repository.SaveChangesAsync(ct);

        return Ok(new
        {
            id = investigation.Id,
            question = investigation.Question,
            status = investigation.Status.ToString(),
            narrative = investigation.NarrativeExplanation,
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

using Aurora.Modules.Graph.Application;
using Aurora.Modules.Scenarios.Application;
using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Web.Models;
using Aurora.Web.Models.Scenarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Aurora.Modules.Identity.Domain;

namespace Aurora.Web.Controllers;

/// <summary>The Scenario Lab (spec section 61).</summary>
[Authorize]
public class ScenariosController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IGraphProjectionService _graphProjection;
    private readonly IScenarioRepository _scenarioRepository;
    private readonly NaturalLanguageScenarioExtractor _nlExtractor;
    private readonly UserManager<ApplicationUser> _userManager;

    public ScenariosController(
        CurrentOrganizationAccessor orgAccessor,
        IGraphProjectionService graphProjection,
        IScenarioRepository scenarioRepository,
        NaturalLanguageScenarioExtractor nlExtractor,
        UserManager<ApplicationUser> userManager)
    {
        _orgAccessor = orgAccessor;
        _graphProjection = graphProjection;
        _scenarioRepository = scenarioRepository;
        _nlExtractor = nlExtractor;
        _userManager = userManager;
    }

    [Authorize(Policy = "Scenario.Read")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        var scenarios = await _scenarioRepository.GetByOrganizationAsync(org.Id, ct);
        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);

        var vm = new ScenarioListViewModel
        {
            Scenarios = scenarios.ToList(),
            NodeNames = snapshot.Nodes.Values.ToDictionary(n => n.Id, n => n.DisplayName)
        };
        return View(vm);
    }

    [Authorize(Policy = "Scenario.Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var vm = new CreateScenarioViewModel
        {
            AvailableNodes = snapshot.Nodes.Values.OrderBy(n => n.DisplayName).Select(n => (n.Id, n.DisplayName)).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Scenario.Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateScenarioViewModel model, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name) || model.TargetNodeId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "A name and target are required.");
            var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
            model.AvailableNodes = snapshot.Nodes.Values.OrderBy(n => n.DisplayName).Select(n => (n.Id, n.DisplayName)).ToList();
            return View(model);
        }

        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var scenario = new Scenario(
            org.Id, model.Name, model.TargetNodeId, model.EventType,
            severity: model.SeverityPercent / 100m, durationDays: model.DurationDays,
            startDateUtc: DateTime.UtcNow, createdByUserId: userId, assumptions: model.Assumptions);

        await _scenarioRepository.AddAsync(scenario, ct);
        await _scenarioRepository.SaveChangesAsync(ct);

        return RedirectToAction("Details", "Simulation", new { scenarioId = scenario.Id });
    }

    /// <summary>Spec section 16: natural-language scenario drafting endpoint used by the AI
    /// command panel. Returns a ScenarioDraft for the user to confirm — it never auto-creates
    /// a scenario, and it flags any field it could not extract instead of guessing.</summary>
    [HttpPost("Scenarios/DraftFromText")]
    [Authorize(Policy = "Scenario.Create")]
    public async Task<IActionResult> DraftFromText([FromBody] NaturalLanguageScenarioRequest request, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var candidates = snapshot.Nodes.Values.ToDictionary(n => n.DisplayName, n => n.Id);

        var draft = _nlExtractor.Extract(request.Text ?? string.Empty, candidates);
        return Ok(draft);
    }
}

using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Simulation.Application;
using Aurora.Modules.Simulation.Contracts;
using Aurora.Modules.Simulation.Domain;
using Aurora.Web.Models;
using Aurora.Web.Models.Simulation;
using System.Diagnostics;
using Aurora.Shared.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>
/// Executes the deterministic Simulation Engine against a Scenario and displays results
/// (spec section 61/62). The run executes inline (the propagation math is fast — milliseconds
/// even for graphs with thousands of nodes) but still reports progress through
/// ISimulationProgressNotifier / SignalR so the UI pattern matches what a longer-running,
/// background-job-backed version (spec section 38) would look like; swapping this action's
/// body for a call to a queued background job is a non-breaking change.
/// </summary>
[Authorize]
public class SimulationController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IGraphProjectionService _graphProjection;
    private readonly IScenarioRepository _scenarioRepository;
    private readonly ISimulationRepository _simulationRepository;
    private readonly SimulationEngine _engine;
    /// <summary>Phases reported to the browser: snapshot loaded, propagation finished, results saved.</summary>
    private const int ProgressPhaseCount = 3;

    private readonly ISimulationProgressNotifier _progressNotifier;
    private readonly AuditRecorder _audit;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(
        CurrentOrganizationAccessor orgAccessor,
        IGraphProjectionService graphProjection,
        IScenarioRepository scenarioRepository,
        ISimulationRepository simulationRepository,
        SimulationEngine engine,
        ISimulationProgressNotifier progressNotifier,
        AuditRecorder audit,
        ILogger<SimulationController> logger)
    {
        _audit = audit;
        _logger = logger;
        _orgAccessor = orgAccessor;
        _graphProjection = graphProjection;
        _scenarioRepository = scenarioRepository;
        _simulationRepository = simulationRepository;
        _engine = engine;
        _progressNotifier = progressNotifier;
    }

    [Authorize(Policy = AuroraPermissions.SimulationRead)]
    public async Task<IActionResult> Details(Guid scenarioId, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var scenario = await _scenarioRepository.GetByIdAsync(org.Id, scenarioId, ct);
        if (scenario is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var runs = await _simulationRepository.GetRunsByOrganizationAsync(org.Id, ct);

        var vm = new ScenarioDetailsViewModel
        {
            Scenario = scenario,
            TargetNodeName = snapshot.Find(scenario.TargetNodeId)?.DisplayName ?? "(unknown node)",
            PastRuns = runs.Where(r => r.ScenarioId == scenarioId).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = AuroraPermissions.SimulationRun)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(Guid scenarioId, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var scenario = await _scenarioRepository.GetByIdAsync(org.Id, scenarioId, ct);
        if (scenario is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        if (snapshot.Find(scenario.TargetNodeId) is null)
        {
            return UnprocessableEntity("The scenario's target no longer exists in the digital twin. Edit the scenario and choose a current target.");
        }

        // The engine is deterministic and draws no random numbers, so no seed is recorded.
        var run = new SimulationRun(org.Id, scenario.Id, randomSeed: 0);
        await _simulationRepository.AddRunAsync(run, ct);
        await _simulationRepository.SaveChangesAsync(ct);

        run.Start();
        scenario.MarkRunning();
        await _progressNotifier.ReportProgressAsync(org.Id, scenario.Id, 1, ProgressPhaseCount, "Graph snapshot loaded.", ct);

        var userId = _orgAccessor.GetCurrentUserId();
        try
        {
            var engineTimer = Stopwatch.StartNew();
            var output = _engine.Run(run.Id, scenario, snapshot);
            AuroraTelemetry.SimulationDurationMs.Record(engineTimer.Elapsed.TotalMilliseconds);

            await _progressNotifier.ReportProgressAsync(org.Id, scenario.Id, 2, ProgressPhaseCount, "Impact propagated through the graph.", ct);

            await _simulationRepository.AddStepsAsync(output.Steps, ct);
            await _simulationRepository.AddResultAsync(output.Result, ct);
            run.Complete();
            scenario.MarkCompleted();
            _audit.Record(org.Id, userId, User.ActorName(), AuditActions.SimulationRun, nameof(SimulationRun), run.Id.ToString(),
                AuditOutcome.Success, $"Model {SimulationEngine.ModelVersion}.");
            await _simulationRepository.SaveChangesAsync(ct);
            await _scenarioRepository.SaveChangesAsync(ct);

            await _progressNotifier.ReportProgressAsync(org.Id, scenario.Id, ProgressPhaseCount, ProgressPhaseCount, "Simulation complete.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Record the failure, then send the user back to the scenario. Exception text stays in the
            // logs and never reaches the browser or the database.
            _logger.LogError(ex, "Simulation run {SimulationRunId} failed for scenario {ScenarioId}.", run.Id, scenario.Id);
            run.Fail("The simulation engine failed. See the server logs for details.");
            scenario.MarkFailed();
            _audit.Record(org.Id, userId, User.ActorName(), AuditActions.SimulationFailed, nameof(SimulationRun), run.Id.ToString(),
                AuditOutcome.Failure, "Simulation engine error.");
            await _simulationRepository.SaveChangesAsync(ct);
            await _scenarioRepository.SaveChangesAsync(ct);

            TempData[FlashKeys.Error] = "The simulation failed and has been recorded. Check the server logs, then try again.";
            return RedirectToAction(nameof(Details), new { scenarioId });
        }

        return RedirectToAction("Results", new { simulationRunId = run.Id });
    }

    [Authorize(Policy = AuroraPermissions.SimulationRead)]
    public async Task<IActionResult> Results(Guid simulationRunId, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var run = await _simulationRepository.GetRunAsync(org.Id, simulationRunId, ct);
        if (run is null) return NotFound();

        var result = await _simulationRepository.GetResultAsync(simulationRunId, ct);
        var steps = await _simulationRepository.GetStepsAsync(simulationRunId, ct);
        var scenario = await _scenarioRepository.GetByIdAsync(org.Id, run.ScenarioId, ct);
        if (scenario is null) return NotFound();

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);

        var dailySeries = steps
            .GroupBy(s => s.DayNumber)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Sum(s => s.ImpactScore), g.Sum(s => s.RevenueImpact)))
            .ToList();

        var topNodes = steps
            .GroupBy(s => s.NodeId)
            .Select(g => (
                NodeName: snapshot.Find(g.Key)?.DisplayName ?? "(unknown)",
                PeakImpact: g.Max(s => s.ImpactScore),
                TotalRevenueImpact: g.Sum(s => s.RevenueImpact)))
            .OrderByDescending(x => x.TotalRevenueImpact)
            .Take(10)
            .ToList();

        var vm = new SimulationResultsViewModel
        {
            Run = run,
            Result = result,
            Scenario = scenario,
            TargetNodeName = snapshot.Find(scenario.TargetNodeId)?.DisplayName ?? "(unknown)",
            DailySeries = dailySeries,
            TopAffectedNodes = topNodes
        };

        return View(vm);
    }
}

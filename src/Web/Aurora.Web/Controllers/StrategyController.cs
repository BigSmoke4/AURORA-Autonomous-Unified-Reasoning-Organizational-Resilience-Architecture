using System.Diagnostics;
using System.Text.Json;
using Aurora.Modules.Analysis.Contracts;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Optimization.Application;
using Aurora.Modules.Optimization.Domain;
using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Simulation.Application;
using Aurora.Shared.Observability;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Strategy;
using Aurora.Web.Services;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>
/// Strategy Lab: counterfactual what-ifs and supply allocation. Results are computed by the
/// deterministic engines and stored with their inputs and model version as an AnalysisRecord.
/// </summary>
[Authorize]
public class StrategyController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IGraphProjectionService _graphProjection;
    private readonly IScenarioRepository _scenarios;
    private readonly CounterfactualAnalyzer _counterfactuals;
    private readonly IAnalysisRecordRepository _analyses;
    private readonly AuditRecorder _audit;
    private readonly GraphSnapshotStore _snapshots;
    private readonly AuroraDbContext _db;

    public StrategyController(
        CurrentOrganizationAccessor orgAccessor,
        IGraphProjectionService graphProjection,
        IScenarioRepository scenarios,
        CounterfactualAnalyzer counterfactuals,
        IAnalysisRecordRepository analyses,
        AuditRecorder audit,
        GraphSnapshotStore snapshots,
        AuroraDbContext db)
    {
        _orgAccessor = orgAccessor;
        _graphProjection = graphProjection;
        _scenarios = scenarios;
        _counterfactuals = counterfactuals;
        _analyses = analyses;
        _audit = audit;
        _snapshots = snapshots;
        _db = db;
    }

    [Authorize(Policy = AuroraPermissions.ScenarioRead)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        return View(await BuildIndexModelAsync(org.Id, new CounterfactualForm(), new OptimizationForm(), ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.SimulationRun)]
    public async Task<IActionResult> Counterfactual([Bind(Prefix = nameof(StrategyIndexViewModel.Counterfactual))] CounterfactualForm form, [FromServices] IValidator<CounterfactualForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(StrategyIndexViewModel.Counterfactual));
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var scenario = await _scenarios.GetByIdAsync(org.Id, form.ScenarioId, ct);
        if (scenario is null) return NotFound();

        GraphSnapshot snapshot;
        DateTime? graphAsOfUtc = null;
        if (form.GraphSnapshotId is { } snapshotId)
        {
            var restored = await _snapshots.RestoreAsync(org.Id, snapshotId, ct);
            if (restored is null)
            {
                ModelState.AddModelError(string.Empty, "The selected graph snapshot was not found.");
                return View("Index", await BuildIndexModelAsync(org.Id, form, new OptimizationForm(), ct));
            }

            snapshot = restored.Value.Graph;
            graphAsOfUtc = restored.Value.Record.CapturedAtUtc;
        }
        else
        {
            snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        }

        if (snapshot.Find(scenario.TargetNodeId) is null)
        {
            ModelState.AddModelError(string.Empty, "The scenario's target is not in the graph used for this analysis.");
        }
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexModelAsync(org.Id, form, new OptimizationForm(), ct));
        }

        var share = form.MitigationSharePct / 100m;
        var result = _counterfactuals.Analyze(scenario, snapshot, share);

        var input = new
        {
            ScenarioId = scenario.Id,
            scenario.Name,
            EventType = scenario.EventType.ToString(),
            scenario.Severity,
            scenario.DurationDays,
            scenario.TargetNodeId,
            MitigationShare = share,
            GraphNodeCount = snapshot.Nodes.Count,
            GraphEdgeCount = snapshot.Edges.Count,
            GraphAsOfUtc = graphAsOfUtc,
        };
        var title = Truncate($"Counterfactual: {scenario.Name} with {form.MitigationSharePct}% diversification", AnalysisRecord.MaxTitleLength);
        var record = await SaveAnalysisAsync(org.Id, AnalysisKind.Counterfactual, title, result.ModelVersion,
            JsonSerializer.Serialize(input), JsonSerializer.Serialize(result), AuditActions.CounterfactualRun, ct);

        return RedirectToAction("Details", "Analysis", new { id = record.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.OptimizationRun)]
    public async Task<IActionResult> Optimize([Bind(Prefix = nameof(StrategyIndexViewModel.Optimization))] OptimizationForm form, [FromServices] IValidator<OptimizationForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(StrategyIndexViewModel.Optimization));
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexModelAsync(org.Id, new CounterfactualForm(), form, ct));
        }

        var (candidates, parseErrors) = CandidateLineParser.Parse(form.CandidatesText);
        if (parseErrors.Count > 0)
        {
            foreach (var error in parseErrors) ModelState.AddModelError(CandidatesModelKey, error);
            return View("Index", await BuildIndexModelAsync(org.Id, new CounterfactualForm(), form, ct));
        }

        var costWeight = form.CostWeightPct / 100m;
        OptimizationResult result;
        try
        {
            var optimizationTimer = Stopwatch.StartNew();
            result = SupplyAllocationOptimizer.Optimize(form.Demand, candidates, costWeight);
            AuroraTelemetry.OptimizationDurationMs.Record(optimizationTimer.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is InvalidOptimizationRequestException or InfeasibleAllocationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", await BuildIndexModelAsync(org.Id, new CounterfactualForm(), form, ct));
        }

        var input = new
        {
            Demand = form.Demand,
            CostWeight = costWeight,
            Candidates = candidates,
        };
        var title = Truncate($"Supply allocation: {form.Demand:N0} units across {candidates.Count} source(s)", AnalysisRecord.MaxTitleLength);
        var record = await SaveAnalysisAsync(org.Id, AnalysisKind.Optimization, title, result.ModelVersion,
            JsonSerializer.Serialize(input), JsonSerializer.Serialize(result), AuditActions.OptimizationRun, ct);

        return RedirectToAction("Details", "Analysis", new { id = record.Id });
    }

    /// <summary>Captures the live graph so that later counterfactuals can run against it as it is now.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.GraphWrite)]
    public async Task<IActionResult> CaptureGraphSnapshot(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var record = await _snapshots.CaptureAsync(org.Id, ct);
        var userId = _orgAccessor.GetCurrentUserId();
        _audit.Record(org.Id, userId, User.ActorName(), AuditActions.GraphSnapshotCaptured, nameof(GraphSnapshotRecord),
            record.Id.ToString(), AuditOutcome.Success, $"{record.NodeCount} nodes, {record.EdgeCount} edges.");
        await _db.SaveChangesAsync(ct);

        TempData[FlashKeys.Success] = "Graph snapshot captured. Counterfactuals can now run against it.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AnalysisRecord> SaveAnalysisAsync(Guid organizationId, AnalysisKind kind, string title, string modelVersion,
        string inputJson, string resultJson, string auditAction, CancellationToken ct)
    {
        var userId = _orgAccessor.GetCurrentUserId() ?? Guid.Empty;
        var record = new AnalysisRecord(organizationId, kind, title, modelVersion, inputJson, resultJson, userId);
        _analyses.Add(record);
        _audit.Record(organizationId, userId, User.ActorName(), auditAction, nameof(AnalysisRecord), record.Id.ToString(),
            AuditOutcome.Success, $"Model {modelVersion}.");
        await _analyses.SaveChangesAsync(ct);
        return record;
    }

    private async Task<StrategyIndexViewModel> BuildIndexModelAsync(Guid organizationId, CounterfactualForm counterfactual,
        OptimizationForm optimization, CancellationToken ct)
    {
        var scenarios = await _scenarios.GetByOrganizationAsync(organizationId, ct);
        var snapshots = await _snapshots.ListRecentAsync(RecentSnapshotCount, ct);
        return new StrategyIndexViewModel
        {
            Scenarios = scenarios,
            GraphSnapshots = snapshots,
            Counterfactual = counterfactual,
            Optimization = optimization,
        };
    }

    private const int RecentSnapshotCount = 10;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static readonly string CandidatesModelKey =
        $"{nameof(StrategyIndexViewModel.Optimization)}.{nameof(OptimizationForm.CandidatesText)}";
}

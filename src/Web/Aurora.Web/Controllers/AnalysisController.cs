using System.Text.Json;
using Aurora.Modules.Analysis.Contracts;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Decisions.Contracts;
using Aurora.Modules.Forecasting.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Optimization.Domain;
using Aurora.Modules.Simulation.Application;
using Aurora.Web.Models;
using Aurora.Web.Models.Analysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Shows one stored analysis: its inputs, its deterministic outputs, and the model version.</summary>
[Authorize(Policy = AuroraPermissions.ReportsRead)]
public class AnalysisController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IAnalysisRecordRepository _analyses;
    private readonly IDecisionRepository _decisions;
    private readonly IAuthorizationService _authorization;

    public AnalysisController(
        CurrentOrganizationAccessor orgAccessor,
        IAnalysisRecordRepository analyses,
        IDecisionRepository decisions,
        IAuthorizationService authorization)
    {
        _orgAccessor = orgAccessor;
        _analyses = analyses;
        _decisions = decisions;
        _authorization = authorization;
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var record = await _analyses.GetByIdAsync(org.Id, id, ct);
        if (record is null) return NotFound();

        CounterfactualComparison? counterfactual = null;
        OptimizationResult? optimization = null;
        ForecastResult? forecast = null;
        Guid? existingDecisionId = null;
        var canPropose = false;

        switch (record.Kind)
        {
            case AnalysisKind.Counterfactual:
                counterfactual = JsonSerializer.Deserialize<CounterfactualComparison>(record.ResultJson);
                var existing = await _decisions.GetBySourceAnalysisAsync(org.Id, record.Id, ct);
                existingDecisionId = existing?.Id;
                canPropose = existing is null
                    && (await _authorization.AuthorizeAsync(User, AuroraPermissions.DecisionPropose)).Succeeded;
                break;
            case AnalysisKind.Optimization:
                optimization = JsonSerializer.Deserialize<OptimizationResult>(record.ResultJson);
                break;
            case AnalysisKind.Forecast:
                forecast = JsonSerializer.Deserialize<ForecastResult>(record.ResultJson);
                break;
        }

        return View(new AnalysisDetailsViewModel
        {
            Record = record,
            Counterfactual = counterfactual,
            Optimization = optimization,
            Forecast = forecast,
            ExistingDecisionId = existingDecisionId,
            CanProposeDecision = canPropose,
        });
    }
}

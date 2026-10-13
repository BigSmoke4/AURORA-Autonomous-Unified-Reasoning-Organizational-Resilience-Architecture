using System.Text.Json;
using Aurora.Modules.Analysis.Contracts;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Decisions.Application;
using Aurora.Modules.Decisions.Contracts;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Simulation.Application;
using Aurora.Web.Models;
using Aurora.Web.Models.Analysis;
using Aurora.Web.Models.Decisions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using FluentValidation;
using Aurora.Web.Validation;
namespace Aurora.Web.Controllers;

/// <summary>
/// Human-in-the-loop decisions (spec sections 31-32). Every state change is a separate, permission-
/// checked, audited action. The proposer cannot approve or reject their own decision.
/// </summary>
[Authorize]
public class DecisionsController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IDecisionRepository _decisions;
    private readonly IAnalysisRecordRepository _analyses;
    private readonly AuditRecorder _audit;
    private readonly DecisionScoringWeights _weights;
    private readonly IAuthorizationService _authorization;

    public DecisionsController(
        CurrentOrganizationAccessor orgAccessor,
        IDecisionRepository decisions,
        IAnalysisRecordRepository analyses,
        AuditRecorder audit,
        DecisionScoringWeights weights,
        IAuthorizationService authorization)
    {
        _orgAccessor = orgAccessor;
        _decisions = decisions;
        _analyses = analyses;
        _audit = audit;
        _weights = weights;
        _authorization = authorization;
    }

    [Authorize(Policy = AuroraPermissions.DecisionRead)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        var queue = await _decisions.GetQueueAsync(org.Id, DecisionQueueLimit, ct);
        return View(new DecisionQueueViewModel { Decisions = queue });
    }

    [Authorize(Policy = AuroraPermissions.DecisionRead)]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var decision = await _decisions.GetByIdAsync(org.Id, id, ct);
        if (decision is null) return NotFound();

        var isProposer = _orgAccessor.GetCurrentUserId() == decision.ProposedByUserId;
        var canApprove = await Can(AuroraPermissions.DecisionApprove);
        var canExecute = await Can(AuroraPermissions.DecisionExecute);

        return View(new DecisionDetailsViewModel
        {
            Decision = decision,
            Factors = ReadFactors(decision.ScoringFactorsJson),
            CanDecide = canApprove && !isProposer && decision.ApprovalState == DecisionApprovalState.PendingApproval,
            CanStartExecution = canExecute
                && decision.ApprovalState == DecisionApprovalState.Approved
                && decision.ExecutionState == DecisionExecutionState.NotStarted,
            CanRecordOutcome = canExecute
                && decision.ApprovalState == DecisionApprovalState.Approved
                && decision.ExecutionState != DecisionExecutionState.Executed,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.DecisionApprove)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        await ChangeApproval(id, approve: true, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.DecisionApprove)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct) =>
        await ChangeApproval(id, approve: false, ct);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.DecisionExecute)]
    public async Task<IActionResult> StartExecution(Guid id, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var decision = await _decisions.GetByIdAsync(org.Id, id, ct);
        if (decision is null) return NotFound();

        try
        {
            decision.StartExecution();
        }
        catch (InvalidOperationException ex)
        {
            TempData[FlashKeys.Error] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        _audit.Record(org.Id, _orgAccessor.GetCurrentUserId(), User.ActorName(), AuditActions.DecisionExecutionStarted,
            nameof(Decision), decision.Id.ToString(), AuditOutcome.Success);
        await _decisions.SaveChangesAsync(ct);
        TempData[FlashKeys.Success] = "Execution started. Record the outcome once the action is complete.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.DecisionExecute)]
    public async Task<IActionResult> RecordOutcome(Guid id, RecordOutcomeForm form, [FromServices] IValidator<RecordOutcomeForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData[FlashKeys.Error] = "Enter the actual revenue impact and a short outcome summary.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var decision = await _decisions.GetByIdAsync(org.Id, id, ct);
        if (decision is null) return NotFound();

        try
        {
            decision.RecordActualOutcome(form.ActualRevenueImpact, form.ActualOutcomeSummary.Trim());
        }
        catch (InvalidOperationException ex)
        {
            TempData[FlashKeys.Error] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        _audit.Record(org.Id, _orgAccessor.GetCurrentUserId(), User.ActorName(), AuditActions.DecisionOutcomeRecorded,
            nameof(Decision), decision.Id.ToString(), AuditOutcome.Success,
            $"Actual revenue impact {form.ActualRevenueImpact:N2}; prediction error {decision.PredictionErrorPct?.ToString("0.00") ?? "n/a"}%.");
        await _decisions.SaveChangesAsync(ct);
        TempData[FlashKeys.Success] = "Outcome recorded and compared with the prediction.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Turns a stored counterfactual analysis into a scored, pending decision.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.DecisionPropose)]
    public async Task<IActionResult> Propose(ProposeDecisionForm form, [FromServices] IValidator<ProposeDecisionForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct);
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData[FlashKeys.Error] = "Check the proposal values: scores are 0-100 and days to benefit is 0-365.";
            return RedirectToAction("Details", "Analysis", new { id = form.AnalysisRecordId });
        }

        var analysis = await _analyses.GetByIdAsync(org.Id, form.AnalysisRecordId, ct);
        if (analysis is null) return NotFound();
        if (analysis.Kind != AnalysisKind.Counterfactual)
        {
            TempData[FlashKeys.Error] = "Only counterfactual analyses can be proposed as decisions.";
            return RedirectToAction("Details", "Analysis", new { id = analysis.Id });
        }

        var existing = await _decisions.GetBySourceAnalysisAsync(org.Id, analysis.Id, ct);
        if (existing is not null)
        {
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

        var comparison = JsonSerializer.Deserialize<CounterfactualComparison>(analysis.ResultJson);
        var userId = _orgAccessor.GetCurrentUserId();
        if (comparison is null || userId is null) return NotFound();

        var decision = DecisionProposalFactory.FromCounterfactual(
            org.Id,
            userId.Value,
            new CounterfactualEvidence(
                analysis.Id,
                analysis.ModelVersion,
                comparison.TargetName,
                comparison.Actual.TotalRevenueImpact,
                comparison.RevenueImpactAvoided,
                comparison.PeakImpactReductionPct),
            new ProposalJudgements(form.OperationalFeasibility, form.StrategicAlignment, form.DaysToBenefit, form.Confidence),
            _weights);

        _decisions.Add(decision);
        _audit.Record(org.Id, userId, User.ActorName(), AuditActions.DecisionProposed, nameof(Decision),
            decision.Id.ToString(), AuditOutcome.Success, $"Proposed from analysis {analysis.Id} with score {decision.DecisionScore:0.00}.");
        await _decisions.SaveChangesAsync(ct);

        TempData[FlashKeys.Success] = "Decision proposed. It is pending approval by someone other than you.";
        return RedirectToAction(nameof(Details), new { id = decision.Id });
    }

    private async Task<IActionResult> ChangeApproval(Guid id, bool approve, CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return NotFound();

        var decision = await _decisions.GetByIdAsync(org.Id, id, ct);
        if (decision is null) return NotFound();

        var approverId = _orgAccessor.GetCurrentUserId();
        if (approverId is null) return Forbid();

        try
        {
            if (approve) decision.Approve(approverId.Value);
            else decision.Reject(approverId.Value);
        }
        catch (InvalidOperationException ex)
        {
            TempData[FlashKeys.Error] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        _audit.Record(org.Id, approverId, User.ActorName(),
            approve ? AuditActions.DecisionApproved : AuditActions.DecisionRejected,
            nameof(Decision), decision.Id.ToString(), AuditOutcome.Success);
        await _decisions.SaveChangesAsync(ct);

        TempData[FlashKeys.Success] = approve ? "Decision approved." : "Decision rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> Can(string policy) =>
        (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

    private static IReadOnlyList<KeyValuePair<string, string>> ReadFactors(string json)
    {
        using var document = JsonDocument.Parse(json);
        var factors = new List<KeyValuePair<string, string>>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                factors.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetDecimal().ToString("0.00")));
            }
        }
        return factors;
    }

    private const int DecisionQueueLimit = 200;
}

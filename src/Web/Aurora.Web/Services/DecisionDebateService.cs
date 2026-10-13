using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Services;

/// <summary>
/// Runs the deterministic decision debate on a decision's recorded figures and keeps every run, so a later verdict can be
/// compared with an earlier one. The counterfactual criterion is read from the decision's source analysis.
/// Stages changes only; the caller commits with SaveChangesAsync.
/// </summary>
public sealed class DecisionDebateService(AuroraDbContext db, TimeProvider clock)
{
    public async Task<DebateSession> RunAsync(Guid organizationId, Decision decision, CancellationToken ct)
    {
        var counterfactualBacked = decision.SourceAnalysisId is { } analysisId
            && await db.AnalysisRecords.AnyAsync(a => a.Id == analysisId && a.Kind == AnalysisKind.Counterfactual, ct);

        var outcome = DecisionDebateEngine.Run(new DebateCase(decision.PredictedRevenueImpact, decision.Confidence,
            decision.DecisionScore, decision.PredictionErrorPct, counterfactualBacked));

        var session = new DebateSession(organizationId, decision.Id, clock.GetUtcNow().UtcDateTime, outcome.AdvocatePoints,
            outcome.CriticPoints, outcome.Verdict, outcome.Rationale);
        db.DebateSessions.Add(session);
        for (var ordinal = 0; ordinal < outcome.Arguments.Count; ordinal++)
        {
            var argument = outcome.Arguments[ordinal];
            db.DebateArguments.Add(new DebateArgument(organizationId, session.Id, ordinal, argument.Role, argument.Supports, argument.Claim));
        }

        return session;
    }

    /// <summary>The most recent debate for a decision, with its arguments in the order they were made.</summary>
    public async Task<(DebateSession Session, IReadOnlyList<DebateArgument> Arguments)?> LatestAsync(Guid decisionId, CancellationToken ct)
    {
        var session = await db.DebateSessions.AsNoTracking()
            .Where(s => s.DecisionId == decisionId)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (session is null) return null;

        var arguments = await db.DebateArguments.AsNoTracking()
            .Where(a => a.DebateSessionId == session.Id)
            .OrderBy(a => a.Ordinal)
            .ToListAsync(ct);
        return (session, arguments);
    }
}

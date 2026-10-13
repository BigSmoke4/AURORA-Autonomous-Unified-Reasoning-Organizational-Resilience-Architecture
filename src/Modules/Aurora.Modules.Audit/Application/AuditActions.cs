namespace Aurora.Modules.Audit.Application;

/// <summary>Stable action names written to the audit trail. Never rename a value once shipped.</summary>
public static class AuditActions
{
    public const string SignInSucceeded = "Identity.SignInSucceeded";
    public const string SignInFailed = "Identity.SignInFailed";
    public const string ScenarioCreated = "Scenario.Created";
    public const string SimulationRun = "Simulation.Run";
    public const string SimulationFailed = "Simulation.Failed";
    public const string CounterfactualRun = "Analysis.CounterfactualRun";
    public const string OptimizationRun = "Analysis.OptimizationRun";
    public const string ForecastRun = "Analysis.ForecastRun";
    public const string DecisionProposed = "Decision.Proposed";
    public const string DecisionApproved = "Decision.Approved";
    public const string DecisionRejected = "Decision.Rejected";
    public const string DecisionExecutionStarted = "Decision.ExecutionStarted";
    public const string DecisionOutcomeRecorded = "Decision.OutcomeRecorded";
    public const string AiInvestigation = "AI.Investigation";
}

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
    public const string WorkforceRoleCreated = "Workforce.RoleCreated";
    public const string FinanceCostCenterCreated = "Finance.CostCenterCreated";
    public const string OperationsProductionRecorded = "Operations.ProductionRecorded";
    public const string TechnologyPatchPolicyCreated = "Technology.PatchPolicyCreated";
    public const string TechnologyPatchRecorded = "Technology.PatchRecorded";
    public const string MarketObservationRecorded = "Markets.ObservationRecorded";
    public const string AssetRegistered = "Assets.Registered";
    public const string ContractRegistered = "Contracts.Registered";
    public const string MetricDefinitionCreated = "Monitoring.MetricDefinitionCreated";
    public const string MetricReadingRecorded = "Monitoring.ReadingRecorded";
    public const string AlertRaised = "Alerts.Raised";
    public const string AlertAcknowledged = "Alerts.Acknowledged";
    public const string AlertResolved = "Alerts.Resolved";
    public const string SupplyPositionRecorded = "SupplyChain.PositionRecorded";
    public const string ReportExported = "Reporting.Exported";
    public const string MetricFeedIngested = "Monitoring.FeedIngested";
    public const string GraphSnapshotCaptured = "Graph.SnapshotCaptured";
    public const string DocumentIndexed = "Knowledge.DocumentIndexed";
    public const string DecisionDebated = "Decision.Debated";
}

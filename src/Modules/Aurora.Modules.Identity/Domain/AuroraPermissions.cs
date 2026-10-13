namespace Aurora.Modules.Identity.Domain;

/// <summary>Granular permission strings (spec section 43), issued to users as claims of type
/// "aurora:permission". Authorization policies in Aurora.Web check for these claims rather
/// than hard-coding role names, so role-to-permission mapping stays centralized and editable.</summary>
public static class AuroraPermissions
{
    public const string GraphRead = "Graph.Read";
    public const string GraphWrite = "Graph.Write";
    public const string ScenarioRead = "Scenario.Read";
    public const string ScenarioCreate = "Scenario.Create";
    public const string ScenarioRun = "Scenario.Run";
    public const string SimulationRead = "Simulation.Read";
    public const string SimulationRun = "Simulation.Run";
    public const string OptimizationRun = "Optimization.Run";
    public const string AiInvestigate = "AI.Investigate";
    public const string AiApprove = "AI.Approve";
    public const string DecisionRead = "Decision.Read";
    public const string DecisionApprove = "Decision.Approve";
    public const string DecisionExecute = "Decision.Execute";
    public const string ReportsRead = "Reports.Read";
    public const string AuditRead = "Audit.Read";
    public const string ForecastRun = "Forecast.Run";
    public const string OptimizationApprove = "Optimization.Approve";
    public const string DecisionPropose = "Decision.Propose";
    public const string WorkforceRead = "Workforce.Read";
    public const string WorkforceManage = "Workforce.Manage";
    public const string FinanceRead = "Finance.Read";
    public const string FinanceManage = "Finance.Manage";
    public const string OperationsRead = "Operations.Read";
    public const string OperationsManage = "Operations.Manage";
    public const string TechnologyRead = "Technology.Read";
    public const string TechnologyManage = "Technology.Manage";
    public const string CustomersRead = "Customers.Read";
    public const string MarketsRead = "Markets.Read";
    public const string MarketsManage = "Markets.Manage";
    public const string AssetsRead = "Assets.Read";
    public const string AssetsManage = "Assets.Manage";
    public const string ContractsRead = "Contracts.Read";
    public const string ContractsManage = "Contracts.Manage";
    public const string MonitoringRead = "Monitoring.Read";
    public const string MonitoringManage = "Monitoring.Manage";
    public const string AlertsRead = "Alerts.Read";
    public const string AlertsAcknowledge = "Alerts.Acknowledge";
    public const string SupplyChainRead = "SupplyChain.Read";
    public const string SupplyChainManage = "SupplyChain.Manage";
    public const string KnowledgeRead = "Knowledge.Read";
    public const string KnowledgeManage = "Knowledge.Manage";

    /// <summary>Every permission AURORA authorizes, used to register one policy per permission.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        GraphRead, GraphWrite, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun,
        OptimizationRun, OptimizationApprove, ForecastRun, AiInvestigate, AiApprove, DecisionRead, DecisionPropose,
        DecisionApprove, DecisionExecute, ReportsRead, AuditRead,
        WorkforceRead, WorkforceManage, FinanceRead, FinanceManage, OperationsRead, OperationsManage,
        TechnologyRead, TechnologyManage, CustomersRead, MarketsRead, MarketsManage, AssetsRead, AssetsManage,
        ContractsRead, ContractsManage, MonitoringRead, MonitoringManage, AlertsRead, AlertsAcknowledge,
        SupplyChainRead, SupplyChainManage, KnowledgeRead, KnowledgeManage
    };

    /// <summary>Default permission grants per role. Seeded on startup; editable later via an
    /// admin screen without a code change once Administration module UI is built.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions = new Dictionary<string, string[]>
    {
        [AuroraRoles.SystemAdministrator] = All.ToArray(),
        [AuroraRoles.Executive] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, OptimizationApprove, ReportsRead, WorkforceRead, FinanceRead, OperationsRead, TechnologyRead, CustomersRead, MarketsRead, AssetsRead, ContractsRead, MonitoringRead, AlertsRead, SupplyChainRead, KnowledgeRead },
        [AuroraRoles.StrategyAnalyst] = new[] { GraphRead, GraphWrite, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun, OptimizationRun, ForecastRun, DecisionRead, DecisionPropose, ReportsRead, FinanceRead, OperationsRead, CustomersRead, MarketsRead, MarketsManage, KnowledgeRead, KnowledgeManage },
        [AuroraRoles.OperationsManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead, OperationsRead, OperationsManage, WorkforceRead, AssetsRead, AssetsManage, MonitoringRead, MonitoringManage, AlertsRead, AlertsAcknowledge, SupplyChainRead },
        [AuroraRoles.SupplyChainManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, SimulationRead, OptimizationRun, DecisionRead, DecisionPropose, ReportsRead, SupplyChainRead, SupplyChainManage, ContractsRead, ContractsManage, MarketsRead, OperationsRead },
        [AuroraRoles.FinanceManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ForecastRun, DecisionRead, ReportsRead, FinanceRead, FinanceManage, ContractsRead, AssetsRead, CustomersRead },
        [AuroraRoles.RiskManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, DecisionRead, DecisionPropose, ReportsRead, AlertsRead, AlertsAcknowledge, MonitoringRead, ContractsRead, CustomersRead, SupplyChainRead },
        [AuroraRoles.TechnologyManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead, TechnologyRead, TechnologyManage, MonitoringRead, MonitoringManage, AlertsRead, AlertsAcknowledge, AssetsRead },
        [AuroraRoles.AiSupervisor] = new[] { GraphRead, ScenarioRead, SimulationRead, AiInvestigate, ReportsRead, AlertsRead, KnowledgeRead, KnowledgeManage },
        [AuroraRoles.DecisionApprover] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, DecisionExecute, ReportsRead },
        [AuroraRoles.Viewer] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead, WorkforceRead, FinanceRead, OperationsRead, TechnologyRead, CustomersRead, MarketsRead, AssetsRead, ContractsRead, MonitoringRead, AlertsRead, SupplyChainRead, KnowledgeRead },
    };
}

/// <summary>Claim types AURORA adds to authenticated principals.</summary>
public static class AuroraClaims
{
    /// <summary>One claim per granted <see cref="AuroraPermissions"/> value, copied from the user's roles.</summary>
    public const string Permission = "aurora:permission";

    /// <summary>The tenant (organization) the user works in. Resolved server-side, never from request input.</summary>
    public const string OrganizationId = "aurora:organizationId";
}

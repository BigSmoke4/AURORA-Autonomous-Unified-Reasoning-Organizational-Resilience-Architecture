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

    /// <summary>Default permission grants per role. Seeded on startup; editable later via an
    /// admin screen without a code change once Administration module UI is built.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions = new Dictionary<string, string[]>
    {
        [AuroraRoles.SystemAdministrator] = new[] { GraphRead, GraphWrite, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun, OptimizationRun, AiInvestigate, AiApprove, DecisionRead, DecisionApprove, DecisionExecute, ReportsRead },
        [AuroraRoles.Executive] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, ReportsRead },
        [AuroraRoles.StrategyAnalyst] = new[] { GraphRead, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun, OptimizationRun, ReportsRead },
        [AuroraRoles.OperationsManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
        [AuroraRoles.SupplyChainManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, SimulationRead, ReportsRead },
        [AuroraRoles.FinanceManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
        [AuroraRoles.RiskManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, ReportsRead },
        [AuroraRoles.TechnologyManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
        [AuroraRoles.AiSupervisor] = new[] { GraphRead, ScenarioRead, SimulationRead, AiInvestigate, ReportsRead },
        [AuroraRoles.DecisionApprover] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, ReportsRead },
        [AuroraRoles.Viewer] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
    };
}

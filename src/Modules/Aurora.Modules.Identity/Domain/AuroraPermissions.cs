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

    /// <summary>Every permission AURORA authorizes, used to register one policy per permission.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        GraphRead, GraphWrite, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun,
        OptimizationRun, OptimizationApprove, ForecastRun, AiInvestigate, AiApprove, DecisionRead, DecisionPropose,
        DecisionApprove, DecisionExecute, ReportsRead, AuditRead
    };

    /// <summary>Default permission grants per role. Seeded on startup; editable later via an
    /// admin screen without a code change once Administration module UI is built.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions = new Dictionary<string, string[]>
    {
        [AuroraRoles.SystemAdministrator] = All.ToArray(),
        [AuroraRoles.Executive] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, OptimizationApprove, ReportsRead },
        [AuroraRoles.StrategyAnalyst] = new[] { GraphRead, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, SimulationRun, OptimizationRun, ForecastRun, DecisionRead, DecisionPropose, ReportsRead },
        [AuroraRoles.OperationsManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
        [AuroraRoles.SupplyChainManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, SimulationRead, OptimizationRun, DecisionRead, DecisionPropose, ReportsRead },
        [AuroraRoles.FinanceManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ForecastRun, DecisionRead, ReportsRead },
        [AuroraRoles.RiskManager] = new[] { GraphRead, ScenarioRead, ScenarioCreate, ScenarioRun, SimulationRead, DecisionRead, DecisionPropose, ReportsRead },
        [AuroraRoles.TechnologyManager] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
        [AuroraRoles.AiSupervisor] = new[] { GraphRead, ScenarioRead, SimulationRead, AiInvestigate, ReportsRead },
        [AuroraRoles.DecisionApprover] = new[] { GraphRead, ScenarioRead, SimulationRead, DecisionRead, DecisionApprove, DecisionExecute, ReportsRead },
        [AuroraRoles.Viewer] = new[] { GraphRead, ScenarioRead, SimulationRead, ReportsRead },
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

namespace Aurora.Modules.Identity.Domain;

/// <summary>The fixed role catalog from spec section 43. Permissions (section 43) are
/// implemented as ASP.NET Core authorization policies in Aurora.Web/Program.cs, each
/// policy requiring one or more of these roles or a granular claim.</summary>
public static class AuroraRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string Executive = "Executive";
    public const string StrategyAnalyst = "StrategyAnalyst";
    public const string OperationsManager = "OperationsManager";
    public const string SupplyChainManager = "SupplyChainManager";
    public const string FinanceManager = "FinanceManager";
    public const string RiskManager = "RiskManager";
    public const string TechnologyManager = "TechnologyManager";
    public const string AiSupervisor = "AiSupervisor";
    public const string DecisionApprover = "DecisionApprover";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SystemAdministrator, Executive, StrategyAnalyst, OperationsManager, SupplyChainManager,
        FinanceManager, RiskManager, TechnologyManager, AiSupervisor, DecisionApprover, Viewer
    };
}

namespace Aurora.Web.Models.CommandCenter;

public class CommandCenterViewModel
{
    public string OrganizationName { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int SupplierCount { get; set; }
    public int FactoryCount { get; set; }
    public int ProductCount { get; set; }
    public int CustomerCount { get; set; }
    public decimal TotalRevenueExposure { get; set; }
    public int CriticalNodeCount { get; set; }
    public int WarningNodeCount { get; set; }
    public int SinglePointsOfFailureCount { get; set; }
    public int ActiveScenarioCount { get; set; }
    public int OpenDecisionCount { get; set; }
}

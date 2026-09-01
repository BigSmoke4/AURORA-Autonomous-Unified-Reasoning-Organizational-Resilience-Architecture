namespace Aurora.Modules.Scenarios.Domain;

/// <summary>Spec section 15 scenario catalog, the subset the simulation engine can execute today.</summary>
public enum ScenarioEventType
{
    CapacityReduction = 0,
    FullShutdown = 1,
    DemandIncrease = 2,
    DemandDecrease = 3,
    CostIncrease = 4
}

public enum ScenarioStatus { Draft = 0, Ready = 1, Running = 2, Completed = 3, Failed = 4 }

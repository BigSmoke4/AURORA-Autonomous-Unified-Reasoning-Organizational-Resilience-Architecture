using Aurora.Modules.Scenarios.Domain;

namespace Aurora.Web.Models.Scenarios;

public class ScenarioListViewModel
{
    public List<Scenario> Scenarios { get; set; } = new();
    public Dictionary<Guid, string> NodeNames { get; set; } = new();
}

public class CreateScenarioViewModel
{
    public Guid TargetNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ScenarioEventType EventType { get; set; }
    public decimal SeverityPercent { get; set; } = 50;
    public int DurationDays { get; set; } = 30;
    public string? Assumptions { get; set; }
    public List<(Guid Id, string Name)> AvailableNodes { get; set; } = new();
}

public class NaturalLanguageScenarioRequest
{
    public string Text { get; set; } = string.Empty;
}

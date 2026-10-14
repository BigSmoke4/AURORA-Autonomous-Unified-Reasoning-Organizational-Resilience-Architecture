using Aurora.Modules.Workforce.Domain;

namespace Aurora.Web.Models.Modules;

public sealed class WorkforceRoleForm
{
    public string Name { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public int RequiredHeadcount { get; set; } = CriticalRole.MinHeadcount;
    public int Criticality { get; set; } = CriticalRole.MinCriticality;
}

public sealed record WorkforceRow(
    string Name,
    string RoleTitle,
    int Criticality,
    int RequiredHeadcount,
    int ActualHeadcount,
    decimal CoveragePct,
    string Status,
    string Tone);

public sealed record WorkforceIndexViewModel(
    IReadOnlyList<WorkforceRow> Rows,
    IReadOnlyList<string> KnownRoleTitles,
    WorkforceRoleForm Form);

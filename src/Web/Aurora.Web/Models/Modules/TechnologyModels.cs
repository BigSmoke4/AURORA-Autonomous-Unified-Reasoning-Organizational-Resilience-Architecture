using System.ComponentModel.DataAnnotations;
using Aurora.Modules.Technology.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Modules;

public sealed class PatchPolicyForm
{
    [Required]
    public Guid? TechnologyAssetId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LastPatchedOn { get; set; }

    [Range(PatchPolicy.MinSlaDays, PatchPolicy.MaxSlaDays)]
    public int PatchSlaDays { get; set; } = PatchPolicy.MinSlaDays;
}

public sealed class RecordPatchForm
{
    [Required]
    public Guid? PatchPolicyId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime PatchedOn { get; set; }
}

public sealed record TechnologyRow(
    Guid AssetId,
    Guid? PolicyId,
    string Asset,
    string AssetType,
    int Criticality,
    DateTime? LastPatchedUtc,
    int? PatchSlaDays,
    int? DaysSincePatch,
    int? DaysOverSla,
    DateTime? DueOnUtc,
    string Status,
    string Tone);

public sealed record TechnologyIndexViewModel(
    IReadOnlyList<TechnologyRow> Rows,
    IReadOnlyList<SelectListItem> Assets,
    PatchPolicyForm Form);

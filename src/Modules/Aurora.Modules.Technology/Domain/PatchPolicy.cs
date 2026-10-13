using Aurora.Shared.Kernel;

namespace Aurora.Modules.Technology.Domain;

/// <summary>
/// The last patch applied to a technology asset and the service-level window it must meet (spec section 22).
/// The optional software product names what the policy covers. Vulnerability advisories for that product are
/// matched against it, so a policy without a product is never linked to an advisory.
/// </summary>
public class PatchPolicy : Entity, ITenantScoped
{
    public const int MinSlaDays = 1;
    public const int MaxSlaDays = 365;
    public const int SoftwareProductMaxLength = 200;

    public Guid OrganizationId { get; private set; }
    public Guid TechnologyAssetId { get; private set; }
    public DateTime LastPatchedOnUtc { get; private set; }
    public int PatchSlaDays { get; private set; }
    public string? SoftwareProduct { get; private set; }

    protected PatchPolicy()
    {
    }

    public PatchPolicy(Guid organizationId, Guid technologyAssetId, DateTime lastPatchedOnUtc, int patchSlaDays,
        string? softwareProduct = null)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        TechnologyAssetId = DomainGuard.RequireId(technologyAssetId, nameof(technologyAssetId));
        LastPatchedOnUtc = DomainGuard.ToUtc(lastPatchedOnUtc);
        PatchSlaDays = DomainGuard.RequireRange(patchSlaDays, MinSlaDays, MaxSlaDays, nameof(patchSlaDays));
        SoftwareProduct = NormalizeProduct(softwareProduct);
    }

    /// <summary>Records a newer patch date. A patch cannot be dated before the one already recorded.</summary>
    public void RecordPatch(DateTime patchedOnUtc)
    {
        var utc = DomainGuard.ToUtc(patchedOnUtc);
        if (utc < LastPatchedOnUtc) throw new InvalidOperationException("A newer patch date cannot precede the recorded one.");
        LastPatchedOnUtc = utc;
        Touch();
    }

    private static string? NormalizeProduct(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return DomainGuard.RequireText(trimmed, SoftwareProductMaxLength, nameof(value));
    }
}

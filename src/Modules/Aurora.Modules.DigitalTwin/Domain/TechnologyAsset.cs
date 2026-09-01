using Aurora.Shared.Kernel;

namespace Aurora.Modules.DigitalTwin.Domain;

public enum TechnologyAssetType { Application = 1, Database = 2, CloudResource = 3 }

public class TechnologyAsset : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TechnologyAssetType AssetType { get; private set; }
    public int Criticality { get; private set; } = 3;
    public OperationalStatus Status { get; private set; } = OperationalStatus.Healthy;

    protected TechnologyAsset() { }

    public TechnologyAsset(Guid organizationId, string name, TechnologyAssetType assetType, int criticality)
    {
        OrganizationId = organizationId;
        Name = name;
        AssetType = assetType;
        Criticality = Math.Clamp(criticality, 1, 5);
    }
}

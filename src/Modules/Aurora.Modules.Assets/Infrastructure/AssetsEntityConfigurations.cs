using Aurora.Modules.Assets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Assets.Infrastructure;

public class PhysicalAssetConfig : IEntityTypeConfiguration<PhysicalAsset>
{
    public void Configure(EntityTypeBuilder<PhysicalAsset> b)
    {
        b.ToTable("PhysicalAssets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(PhysicalAsset.NameMaxLength);
        b.Property(x => x.Category).IsRequired().HasMaxLength(PhysicalAsset.CategoryMaxLength);
        b.Property(x => x.AcquisitionCost).HasColumnType("decimal(18,2)");
        b.Property(x => x.ResidualValue).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

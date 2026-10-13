using Aurora.Modules.SupplyChain.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.SupplyChain.Infrastructure;

public class InventoryPositionConfig : IEntityTypeConfiguration<InventoryPosition>
{
    public void Configure(EntityTypeBuilder<InventoryPosition> b)
    {
        b.ToTable("InventoryPositions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).IsRequired().HasMaxLength(InventoryPosition.SkuMaxLength);
        b.Property(x => x.Location).IsRequired().HasMaxLength(InventoryPosition.LocationMaxLength);
        b.Property(x => x.AverageDailyDemand).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.OrganizationId);
    }
}

using Aurora.Modules.Markets.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Markets.Infrastructure;

public class MarketObservationConfig : IEntityTypeConfiguration<MarketObservation>
{
    public void Configure(EntityTypeBuilder<MarketObservation> b)
    {
        b.ToTable("MarketObservations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Market).IsRequired().HasMaxLength(MarketObservation.MarketMaxLength);
        b.Property(x => x.DemandIndex).HasColumnType("decimal(18,4)");
        b.Property(x => x.PriceIndex).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.OrganizationId);
    }
}

using Aurora.Modules.Risk.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Risk.Infrastructure;

public class RiskRecordConfig : IEntityTypeConfiguration<RiskRecord>
{
    public void Configure(EntityTypeBuilder<RiskRecord> b)
    {
        b.ToTable("RiskRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Probability).HasColumnType("decimal(4,3)");
        b.Property(x => x.Impact).HasColumnType("decimal(4,3)");
        b.Property(x => x.Exposure).HasColumnType("decimal(4,3)");
        b.Property(x => x.Score).HasColumnType("decimal(6,2)");
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.NodeId);
    }
}

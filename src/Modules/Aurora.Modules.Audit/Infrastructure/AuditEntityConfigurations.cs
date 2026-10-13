using Aurora.Modules.Audit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Audit.Infrastructure;

public class AuditEntryConfig : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorName).IsRequired().HasMaxLength(256);
        b.Property(x => x.Action).IsRequired().HasMaxLength(120);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(120);
        b.Property(x => x.EntityId).HasMaxLength(120);
        b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
    }
}

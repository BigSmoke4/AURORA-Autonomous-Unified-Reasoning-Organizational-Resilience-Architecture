using Aurora.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Decisions.Infrastructure;

public class DecisionConfig : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> b)
    {
        b.ToTable("Decisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Recommendation).HasMaxLength(2000);
        b.Property(x => x.EvidenceSummary).HasMaxLength(4000);
        b.Property(x => x.DecisionScore).HasColumnType("decimal(6,2)");
        b.Property(x => x.Confidence).HasColumnType("decimal(6,2)");
        b.HasIndex(x => x.OrganizationId);
        b.Ignore(x => x.DomainEvents);
    }
}

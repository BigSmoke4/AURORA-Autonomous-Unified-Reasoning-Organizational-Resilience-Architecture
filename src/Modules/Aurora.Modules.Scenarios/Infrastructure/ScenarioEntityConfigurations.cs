using Aurora.Modules.Scenarios.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Scenarios.Infrastructure;

public class ScenarioConfig : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> b)
    {
        b.ToTable("Scenarios");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Severity).HasColumnType("decimal(4,3)");
        b.Property(x => x.Assumptions).HasMaxLength(2000);
        b.HasIndex(x => x.OrganizationId);
        b.Ignore(x => x.DomainEvents);
    }
}

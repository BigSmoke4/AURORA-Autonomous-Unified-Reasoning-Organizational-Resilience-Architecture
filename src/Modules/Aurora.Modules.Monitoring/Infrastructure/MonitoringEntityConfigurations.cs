using Aurora.Modules.Monitoring.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Monitoring.Infrastructure;

public class MetricDefinitionConfig : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> b)
    {
        b.ToTable("MetricDefinitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(MetricDefinition.NameMaxLength);
        b.Property(x => x.Unit).IsRequired().HasMaxLength(MetricDefinition.UnitMaxLength);
        b.Property(x => x.MinimumThreshold).HasColumnType("decimal(18,4)");
        b.Property(x => x.MaximumThreshold).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class MetricReadingConfig : IEntityTypeConfiguration<MetricReading>
{
    public void Configure(EntityTypeBuilder<MetricReading> b)
    {
        b.ToTable("MetricReadings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasColumnType("decimal(18,4)");
        b.HasIndex(x => x.OrganizationId);
    }
}

using Aurora.Modules.Alerts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Alerts.Infrastructure;

public class AlertConfig : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("Alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceKey).IsRequired().HasMaxLength(Alert.SourceKeyMaxLength);
        b.Property(x => x.Title).IsRequired().HasMaxLength(Alert.TitleMaxLength);
        b.Property(x => x.Detail).HasMaxLength(Alert.DetailMaxLength);
        b.HasIndex(x => x.OrganizationId);
    }
}

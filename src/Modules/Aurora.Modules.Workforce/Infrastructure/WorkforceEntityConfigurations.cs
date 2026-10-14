using Aurora.Modules.Workforce.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Workforce.Infrastructure;

public class CriticalRoleConfig : IEntityTypeConfiguration<CriticalRole>
{
    public void Configure(EntityTypeBuilder<CriticalRole> b)
    {
        b.ToTable("CriticalRoles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(CriticalRole.NameMaxLength);
        b.Property(x => x.RoleTitle).IsRequired().HasMaxLength(CriticalRole.NameMaxLength);
        b.HasIndex(x => x.OrganizationId);
    }
}

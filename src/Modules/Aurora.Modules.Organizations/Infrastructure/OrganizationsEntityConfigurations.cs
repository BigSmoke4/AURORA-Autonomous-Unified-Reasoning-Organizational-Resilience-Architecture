using Aurora.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Organizations.Infrastructure;

public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Industry).HasMaxLength(200);
        b.Property(x => x.HeadquartersLocation).HasMaxLength(300);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.Name);
        b.Ignore(x => x.DomainEvents);
        b.Metadata.FindNavigation(nameof(Organization.BusinessUnits))?
            .SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

public class BusinessUnitConfig : IEntityTypeConfiguration<BusinessUnit>
{
    public void Configure(EntityTypeBuilder<BusinessUnit> b)
    {
        b.ToTable("BusinessUnits");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.OrganizationId);
        b.HasOne<Organization>().WithMany(o => o.BusinessUnits).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.BusinessUnitId);
        b.HasOne<BusinessUnit>().WithMany(bu => bu.Departments).HasForeignKey(x => x.BusinessUnitId).OnDelete(DeleteBehavior.Cascade);
    }
}

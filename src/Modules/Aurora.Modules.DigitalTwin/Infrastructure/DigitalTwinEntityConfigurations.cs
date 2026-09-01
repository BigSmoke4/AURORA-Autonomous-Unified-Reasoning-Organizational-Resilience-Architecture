using Aurora.Modules.DigitalTwin.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.DigitalTwin.Infrastructure;

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        b.Property(x => x.Role).HasMaxLength(200);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.DepartmentId);
    }
}

public class SupplierConfig : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Category).HasMaxLength(100);
        b.Property(x => x.CapacityUtilizationPct).HasColumnType("decimal(5,2)");
        b.Property(x => x.AnnualRevenueExposure).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class FactoryConfig : IEntityTypeConfiguration<Factory>
{
    public void Configure(EntityTypeBuilder<Factory> b)
    {
        b.ToTable("Factories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.CapacityUtilizationPct).HasColumnType("decimal(5,2)");
        b.Property(x => x.AnnualRevenueExposure).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class WarehouseConfig : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("Warehouses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.CapacityUtilizationPct).HasColumnType("decimal(5,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Sku).HasMaxLength(60);
        b.Property(x => x.AnnualRevenueExposure).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Segment).HasMaxLength(100);
        b.Property(x => x.AnnualRevenueExposure).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

public class TechnologyAssetConfig : IEntityTypeConfiguration<TechnologyAsset>
{
    public void Configure(EntityTypeBuilder<TechnologyAsset> b)
    {
        b.ToTable("TechnologyAssets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.OrganizationId);
    }
}

using Aurora.Modules.Finance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Finance.Infrastructure;

public class CostCenterConfig : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> b)
    {
        b.ToTable("CostCenters");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).IsRequired().HasMaxLength(CostCenter.CodeMaxLength);
        b.Property(x => x.Name).IsRequired().HasMaxLength(CostCenter.NameMaxLength);
        b.Property(x => x.AnnualBudget).HasColumnType("decimal(18,2)");
        b.Property(x => x.ActualSpend).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

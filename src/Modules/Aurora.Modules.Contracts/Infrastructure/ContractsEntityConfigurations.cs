using Aurora.Modules.Contracts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Contracts.Infrastructure;

public class SupplierContractConfig : IEntityTypeConfiguration<SupplierContract>
{
    public void Configure(EntityTypeBuilder<SupplierContract> b)
    {
        b.ToTable("SupplierContracts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reference).IsRequired().HasMaxLength(SupplierContract.ReferenceMaxLength);
        b.Property(x => x.Counterparty).IsRequired().HasMaxLength(SupplierContract.CounterpartyMaxLength);
        b.Property(x => x.AnnualValue).HasColumnType("decimal(18,2)");
        b.Property(x => x.LatePenaltyPerDay).HasColumnType("decimal(18,2)");
        b.HasIndex(x => x.OrganizationId);
    }
}

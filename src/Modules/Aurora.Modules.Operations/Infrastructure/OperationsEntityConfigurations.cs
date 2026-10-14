using Aurora.Modules.Operations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Operations.Infrastructure;

public class ProductionRecordConfig : IEntityTypeConfiguration<ProductionRecord>
{
    public void Configure(EntityTypeBuilder<ProductionRecord> b)
    {
        b.ToTable("ProductionRecords");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OrganizationId);
    }
}

using Aurora.Modules.Analysis.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Analysis.Infrastructure;

public class AnalysisRecordConfig : IEntityTypeConfiguration<AnalysisRecord>
{
    public void Configure(EntityTypeBuilder<AnalysisRecord> b)
    {
        b.ToTable("AnalysisRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Title).IsRequired().HasMaxLength(AnalysisRecord.MaxTitleLength);
        b.Property(x => x.ModelVersion).IsRequired().HasMaxLength(AnalysisRecord.MaxModelVersionLength);
        b.Property(x => x.InputJson).IsRequired().HasColumnType("text");
        b.Property(x => x.ResultJson).IsRequired().HasColumnType("text");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
    }
}

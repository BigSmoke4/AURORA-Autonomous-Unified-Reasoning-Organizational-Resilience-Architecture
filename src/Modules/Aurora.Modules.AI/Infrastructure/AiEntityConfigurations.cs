using Aurora.Modules.AI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.AI.Infrastructure;

public class AiInvestigationConfig : IEntityTypeConfiguration<AiInvestigation>
{
    public void Configure(EntityTypeBuilder<AiInvestigation> b)
    {
        b.ToTable("AiInvestigations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Question).IsRequired().HasMaxLength(AiInvestigation.MaxQuestionLength);
        b.Property(x => x.NarrativeExplanation).HasMaxLength(4000);
        b.Property(x => x.Confidence).HasColumnType("decimal(5,2)");
        b.Property(x => x.NarrativeSource).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ModelVersion).IsRequired().HasMaxLength(AiInvestigation.MaxModelVersionLength);
        b.HasIndex(x => x.OrganizationId);
        b.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.AiInvestigationId).OnDelete(DeleteBehavior.Cascade);
        b.Metadata.FindNavigation(nameof(AiInvestigation.Evidence))!
            .SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

public class AiEvidenceConfig : IEntityTypeConfiguration<AiEvidence>
{
    public void Configure(EntityTypeBuilder<AiEvidence> b)
    {
        b.ToTable("AiEvidence");
        b.HasKey(x => x.Id);
        b.Property(x => x.ToolName).HasMaxLength(200);
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.Property(x => x.RawDataJson).HasColumnType("jsonb");
        b.HasIndex(x => x.AiInvestigationId);
    }
}

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
        b.Property(x => x.Question).IsRequired().HasMaxLength(1000);
        b.Property(x => x.NarrativeExplanation).HasMaxLength(4000);
        b.Property(x => x.Confidence).HasColumnType("decimal(5,2)");
        b.HasIndex(x => x.OrganizationId);
        b.Ignore(x => x.DomainEvents);
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

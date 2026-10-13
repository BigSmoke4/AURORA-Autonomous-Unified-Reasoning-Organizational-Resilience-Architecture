using Aurora.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Decisions.Infrastructure;

public class DecisionConfig : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> b)
    {
        b.ToTable("Decisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(Decision.MaxTitleLength);
        b.Property(x => x.Recommendation).HasMaxLength(Decision.MaxRecommendationLength);
        b.Property(x => x.EvidenceSummary).HasMaxLength(Decision.MaxEvidenceLength);
        b.Property(x => x.DecisionScore).HasColumnType("decimal(6,2)");
        b.Property(x => x.Confidence).HasColumnType("decimal(6,2)");
        b.Property(x => x.SourceKind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ModelVersion).IsRequired().HasMaxLength(60);
        b.Property(x => x.PredictedRevenueImpact).HasColumnType("decimal(18,2)");
        b.Property(x => x.ActualRevenueImpact).HasColumnType("decimal(18,2)");
        b.Property(x => x.PredictionErrorPct).HasColumnType("decimal(9,2)");
        b.Property(x => x.ScoringFactorsJson).IsRequired().HasColumnType("text");
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.ApprovalState });
    }
}

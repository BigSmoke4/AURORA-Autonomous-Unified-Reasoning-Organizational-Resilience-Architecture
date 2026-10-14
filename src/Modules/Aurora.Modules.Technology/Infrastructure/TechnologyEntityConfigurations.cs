using Aurora.Modules.Technology.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Technology.Infrastructure;

public class PatchPolicyConfig : IEntityTypeConfiguration<PatchPolicy>
{
    public void Configure(EntityTypeBuilder<PatchPolicy> b)
    {
        b.ToTable("TechnologyPatchPolicies");
        b.HasKey(x => x.Id);
        b.Property(x => x.SoftwareProduct).HasMaxLength(PatchPolicy.SoftwareProductMaxLength);
        b.HasIndex(x => x.OrganizationId);
    }
}

public class VulnerabilityAdvisoryConfig : IEntityTypeConfiguration<VulnerabilityAdvisory>
{
    public void Configure(EntityTypeBuilder<VulnerabilityAdvisory> b)
    {
        b.ToTable("VulnerabilityAdvisories");
        b.HasKey(x => x.Id);
        b.Property(x => x.CveId).IsRequired().HasMaxLength(VulnerabilityAdvisory.CveIdMaxLength);
        b.Property(x => x.ProductName).IsRequired().HasMaxLength(VulnerabilityAdvisory.ProductMaxLength);
        b.Property(x => x.CvssScore).HasColumnType("decimal(3,1)");
        b.Property(x => x.Summary).IsRequired().HasMaxLength(VulnerabilityAdvisory.SummaryMaxLength);
        b.HasIndex(x => new { x.OrganizationId, x.CveId }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.ProductName });
    }
}

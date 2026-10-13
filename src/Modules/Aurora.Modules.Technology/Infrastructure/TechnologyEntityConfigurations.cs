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
        b.HasIndex(x => x.OrganizationId);
    }
}

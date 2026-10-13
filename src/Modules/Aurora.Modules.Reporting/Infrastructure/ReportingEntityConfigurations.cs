using Aurora.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Reporting.Infrastructure;

public sealed class ExecutiveReportRunConfig : IEntityTypeConfiguration<ExecutiveReportRun>
{
    public void Configure(EntityTypeBuilder<ExecutiveReportRun> b)
    {
        b.ToTable("ExecutiveReportRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestedByName).IsRequired().HasMaxLength(ExecutiveReportRun.RequestedByNameMaxLength);
        b.Property(x => x.ContentSha256).IsRequired().HasMaxLength(ExecutiveReportRun.ContentHashLength);
        b.HasIndex(x => new { x.OrganizationId, x.GeneratedAtUtc });
    }
}

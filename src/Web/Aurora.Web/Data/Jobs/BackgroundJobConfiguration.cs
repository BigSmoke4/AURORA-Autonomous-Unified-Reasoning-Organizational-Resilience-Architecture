using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Web.Data.Jobs;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> b)
    {
        b.ToTable("BackgroundJobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).IsRequired().HasMaxLength(BackgroundJob.KindMaxLength);
        b.Property(x => x.PayloadJson).IsRequired().HasMaxLength(BackgroundJob.PayloadMaxLength);
        b.Property(x => x.ResultSummary).HasMaxLength(BackgroundJob.ResultMaxLength);
        b.Property(x => x.LastError).HasMaxLength(BackgroundJob.ErrorMaxLength);
        b.HasIndex(x => new { x.Status, x.AvailableAtUtc });
        b.HasIndex(x => x.OrganizationId);
    }
}

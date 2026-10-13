using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Web.Data.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).IsRequired().HasMaxLength(OutboxMessage.EventTypeMaxLength);
        b.Property(x => x.Payload).IsRequired().HasMaxLength(OutboxMessage.PayloadMaxLength);
        b.Property(x => x.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.ProcessedOnUtc);
    }
}

using Aurora.Modules.AI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.AI.Infrastructure;

public sealed class KnowledgeDocumentConfig : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.ToTable("KnowledgeDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(KnowledgeDocument.TitleMaxLength);
        b.Property(x => x.SourceName).IsRequired().HasMaxLength(KnowledgeDocument.SourceNameMaxLength);
        b.HasIndex(x => x.OrganizationId);
    }
}

public sealed class DocumentChunkConfig : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("DocumentChunks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Text).IsRequired().HasMaxLength(DocumentChunk.MaxTextLength);
        b.HasIndex(x => new { x.OrganizationId, x.KnowledgeDocumentId, x.Ordinal });
    }
}

public sealed class DebateSessionConfig : IEntityTypeConfiguration<DebateSession>
{
    public void Configure(EntityTypeBuilder<DebateSession> b)
    {
        b.ToTable("DebateSessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Verdict).IsRequired().HasMaxLength(DebateSession.VerdictMaxLength);
        b.Property(x => x.VerdictRationale).IsRequired().HasMaxLength(DebateSession.RationaleMaxLength);
        b.HasIndex(x => new { x.OrganizationId, x.DecisionId, x.StartedAtUtc });
    }
}

public sealed class DebateArgumentConfig : IEntityTypeConfiguration<DebateArgument>
{
    public void Configure(EntityTypeBuilder<DebateArgument> b)
    {
        b.ToTable("DebateArguments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.Claim).IsRequired().HasMaxLength(DebateArgument.ClaimMaxLength);
        b.HasIndex(x => new { x.OrganizationId, x.DebateSessionId, x.Ordinal });
    }
}

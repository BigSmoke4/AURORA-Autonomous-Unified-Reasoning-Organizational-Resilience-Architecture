using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

/// <summary>An organizational document (policy, contract summary, incident report) indexed for retrieval.</summary>
public class KnowledgeDocument : Entity, ITenantScoped
{
    public const int TitleMaxLength = 200;
    public const int SourceNameMaxLength = 200;
    public const int MaxBodyLength = 100_000;

    /// <summary>
    /// Retrieval loads every chunk of an organization into memory, so the corpus has a ceiling. At 800 characters a
    /// chunk this is about 4 MB of text per organization.
    /// </summary>
    public const int MaxChunksPerOrganization = 5_000;

    public Guid OrganizationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string SourceName { get; private set; } = string.Empty;
    public int ChunkCount { get; private set; }

    protected KnowledgeDocument()
    {
    }

    public KnowledgeDocument(Guid organizationId, string title, string sourceName, int chunkCount)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Title = DomainGuard.RequireText(title, TitleMaxLength, nameof(title));
        SourceName = DomainGuard.RequireText(sourceName, SourceNameMaxLength, nameof(sourceName));
        ChunkCount = DomainGuard.RequireRange(chunkCount, 1, int.MaxValue, nameof(chunkCount));
    }

    public static bool FitsOrganizationLimit(int existingChunks, int newChunks) =>
        existingChunks + newChunks <= MaxChunksPerOrganization;
}

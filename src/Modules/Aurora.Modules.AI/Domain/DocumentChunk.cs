using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

/// <summary>A contiguous passage of a document. Retrieval scores and returns chunks, never whole documents.</summary>
public class DocumentChunk : Entity, ITenantScoped
{
    public const int MaxTextLength = 2000;

    public Guid OrganizationId { get; private set; }
    public Guid KnowledgeDocumentId { get; private set; }
    public int Ordinal { get; private set; }
    public string Text { get; private set; } = string.Empty;

    protected DocumentChunk()
    {
    }

    public DocumentChunk(Guid organizationId, Guid knowledgeDocumentId, int ordinal, string text)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        KnowledgeDocumentId = DomainGuard.RequireId(knowledgeDocumentId, nameof(knowledgeDocumentId));
        Ordinal = DomainGuard.RequireRange(ordinal, 0, int.MaxValue, nameof(ordinal));
        Text = DomainGuard.RequireText(text, MaxTextLength, nameof(text));
    }
}

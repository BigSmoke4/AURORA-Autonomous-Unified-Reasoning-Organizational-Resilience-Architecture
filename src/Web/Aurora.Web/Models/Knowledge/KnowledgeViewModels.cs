using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;

namespace Aurora.Web.Models.Knowledge;

public sealed class KnowledgeIndexViewModel
{
    public IReadOnlyList<KnowledgeDocument> Documents { get; init; } = Array.Empty<KnowledgeDocument>();
    public string Query { get; init; } = string.Empty;
    public bool SearchPerformed { get; init; }
    public IReadOnlyList<RetrievalHit> Hits { get; init; } = Array.Empty<RetrievalHit>();
    public DocumentForm Form { get; init; } = new();
}

public sealed class DocumentForm
{
    public string Title { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>A document's id and title, used to label retrieved chunks.</summary>
public sealed record DocumentTitle(Guid Id, string Title);

using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Data;
using Aurora.Web.Models;
using Aurora.Web.Models.Knowledge;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Controllers;

/// <summary>
/// Organization documents indexed for retrieval. Retrieval ranks chunks with BM25 and returns excerpts with their
/// document and position. It returns no generated text, and no model reads a document in order to answer.
/// </summary>
[Authorize(Policy = AuroraPermissions.KnowledgeRead)]
public class KnowledgeController : ModuleControllerBase
{
    public const int MaxQueryLength = 200;
    private const int RecentDocumentCount = 20;

    public KnowledgeController(CurrentOrganizationAccessor organizations, AuroraDbContext db, AuditRecorder audit, TimeProvider clock)
        : base(organizations, db, audit, clock)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken ct)
    {
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var query = q?.Trim() ?? string.Empty;
        if (query.Length > MaxQueryLength)
        {
            ModelState.AddModelError(string.Empty, $"The search can be at most {MaxQueryLength} characters.");
            return View(await BuildViewModelAsync(string.Empty, new DocumentForm(), Array.Empty<RetrievalHit>(), false, ct));
        }

        var hits = query.Length == 0 ? Array.Empty<RetrievalHit>() : await SearchAsync(query, ct);
        return View(await BuildViewModelAsync(query, new DocumentForm(), hits, query.Length > 0, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuroraPermissions.KnowledgeManage)]
    public async Task<IActionResult> Upload([Bind(Prefix = nameof(KnowledgeIndexViewModel.Form))] DocumentForm form, [FromServices] IValidator<DocumentForm> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(form, ModelState, ct, nameof(KnowledgeIndexViewModel.Form));
        var org = await CurrentOrganizationAsync(ct);
        if (org is null) return NotFound();

        var chunks = ModelState.IsValid ? DocumentChunker.Split(form.Body) : Array.Empty<string>();
        if (ModelState.IsValid && chunks.Count == 0)
        {
            ModelState.AddModelError(nameof(DocumentForm.Body), "The document has no text to index.");
        }

        if (ModelState.IsValid)
        {
            var existingChunks = await Db.Set<DocumentChunk>().CountAsync(ct);
            if (!KnowledgeDocument.FitsOrganizationLimit(existingChunks, chunks.Count))
            {
                ModelState.AddModelError(nameof(DocumentForm.Body),
                    $"This organization's knowledge base holds at most {KnowledgeDocument.MaxChunksPerOrganization} passages.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildViewModelAsync(string.Empty, form, Array.Empty<RetrievalHit>(), false, ct));
        }

        var document = new KnowledgeDocument(org.Id, form.Title.Trim(), form.SourceName.Trim(), chunks.Count);
        Entities<KnowledgeDocument>().Add(document);
        for (var ordinal = 0; ordinal < chunks.Count; ordinal++)
        {
            Entities<DocumentChunk>().Add(new DocumentChunk(org.Id, document.Id, ordinal, chunks[ordinal]));
        }

        RecordAudit(org, AuditActions.DocumentIndexed, nameof(KnowledgeDocument), document.Id.ToString(), $"{chunks.Count} chunk(s).");
        await Db.SaveChangesAsync(ct);
        Success($"Indexed {chunks.Count} passage(s) from '{document.Title}'.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<RetrievalHit>> SearchAsync(string query, CancellationToken ct)
    {
        var titles = (await Entities<KnowledgeDocument>().SelectAsync(d => new DocumentTitle(d.Id, d.Title), null, null, ct))
            .ToDictionary(t => t.Id);
        var chunks = await Entities<DocumentChunk>().ListAsync(null,
            q => q.OrderBy(c => c.KnowledgeDocumentId).ThenBy(c => c.Ordinal), ct);

        var candidates = chunks
            .Where(c => titles.ContainsKey(c.KnowledgeDocumentId))
            .Select(c => new RetrievalCandidate(c.Id, c.KnowledgeDocumentId, titles[c.KnowledgeDocumentId].Title, c.Ordinal, c.Text))
            .ToList();
        return DocumentRetriever.Search(query, candidates);
    }

    private async Task<KnowledgeIndexViewModel> BuildViewModelAsync(string query, DocumentForm form, IReadOnlyList<RetrievalHit> hits,
        bool searchPerformed, CancellationToken ct)
    {
        var documents = await Entities<KnowledgeDocument>().ListAsync(null, q => q.OrderByDescending(d => d.CreatedAtUtc), ct,
            RecentDocumentCount);
        return new KnowledgeIndexViewModel
        {
            Documents = documents,
            Query = query,
            SearchPerformed = searchPerformed,
            Hits = hits,
            Form = form,
        };
    }
}

using Aurora.Modules.AI.Domain;
using Aurora.Web.Models.Knowledge;
using FluentValidation;

namespace Aurora.Web.Validation;

public sealed class DocumentFormValidator : AbstractValidator<DocumentForm>
{
    public DocumentFormValidator()
    {
        RuleFor(f => f.Title).NotEmpty().MaximumLength(KnowledgeDocument.TitleMaxLength);
        RuleFor(f => f.SourceName).NotEmpty().MaximumLength(KnowledgeDocument.SourceNameMaxLength);
        RuleFor(f => f.Body).NotEmpty().MaximumLength(KnowledgeDocument.MaxBodyLength);
    }
}

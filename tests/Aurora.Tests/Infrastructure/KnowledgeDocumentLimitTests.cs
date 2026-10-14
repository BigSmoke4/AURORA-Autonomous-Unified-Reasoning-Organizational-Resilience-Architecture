using Aurora.Modules.AI.Domain;
using Xunit;

namespace Aurora.Tests.Infrastructure;

public sealed class KnowledgeDocumentLimitTests
{
    [Fact]
    public void AnOrganizationAtTheLimit_CannotAddAnotherPassage()
    {
        Assert.True(KnowledgeDocument.FitsOrganizationLimit(KnowledgeDocument.MaxChunksPerOrganization - 1, 1));
        Assert.False(KnowledgeDocument.FitsOrganizationLimit(KnowledgeDocument.MaxChunksPerOrganization, 1));
    }

    [Fact]
    public void ADocumentThatWouldCrossTheLimit_IsRefusedAsAWhole()
    {
        Assert.False(KnowledgeDocument.FitsOrganizationLimit(KnowledgeDocument.MaxChunksPerOrganization - 2, 3));
    }
}

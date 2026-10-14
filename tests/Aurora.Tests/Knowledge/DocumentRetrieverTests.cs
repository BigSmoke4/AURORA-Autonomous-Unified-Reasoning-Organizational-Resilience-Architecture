using Aurora.Modules.AI.Application;
using Xunit;

namespace Aurora.Tests.Knowledge;

public sealed class DocumentRetrieverTests
{
    private static RetrievalCandidate Chunk(string title, int ordinal, string text) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, ordinal, text);

    [Fact]
    public void TokenizerKeepsWordsOfTwoOrMoreLettersAndDigits()
    {
        Assert.Equal(new[] { "line", "cycle", "time" }, DocumentRetriever.Tokenize("Line 1 cycle-time"));
    }

    [Fact]
    public void ChunkContainingMoreQueryTermsRanksFirst()
    {
        var both = Chunk("Ops", 0, "Cycle time rose after the coolant change.");
        var one = Chunk("Ops", 1, "Cycle counts are logged daily.");
        var none = Chunk("Ops", 2, "Unrelated text about payroll.");

        var hits = DocumentRetriever.Search("cycle time", new[] { one, both, none });

        Assert.Equal(2, hits.Count);
        Assert.Equal(both.ChunkId, hits[0].ChunkId);
        Assert.Equal(one.ChunkId, hits[1].ChunkId);
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public void EqualScoresAreOrderedByTitleThenPosition()
    {
        var beta = Chunk("Beta", 0, "Coolant limits apply.");
        var alphaLater = Chunk("Alpha", 3, "Coolant limits apply.");
        var alphaFirst = Chunk("Alpha", 1, "Coolant limits apply.");

        var hits = DocumentRetriever.Search("coolant", new[] { beta, alphaLater, alphaFirst });

        Assert.Equal(new[] { alphaFirst.ChunkId, alphaLater.ChunkId, beta.ChunkId }, hits.Select(h => h.ChunkId).ToArray());
    }

    [Fact]
    public void LimitCapsTheNumberOfResults()
    {
        var chunks = Enumerable.Range(0, 8).Select(i => Chunk("Doc", i, "supplier risk review")).ToList();

        Assert.Single(DocumentRetriever.Search("supplier", chunks, limit: 1));
    }

    [Fact]
    public void BlankQueryAndNoMatchReturnNothing()
    {
        var chunk = Chunk("Doc", 0, "supplier risk review");

        Assert.Empty(DocumentRetriever.Search("   ", new[] { chunk }));
        Assert.Empty(DocumentRetriever.Search("payroll", new[] { chunk }));
    }

    [Fact]
    public void LongExcerptsAreShortenedWithAnEllipsis()
    {
        var text = string.Concat(Enumerable.Repeat("supplier note ", 40));

        var hit = Assert.Single(DocumentRetriever.Search("supplier", new[] { Chunk("Doc", 0, text) }));

        Assert.True(hit.Snippet.Length <= DocumentRetriever.SnippetLength + 1);
        Assert.EndsWith("…", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void LimitMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentRetriever.Search("x", Array.Empty<RetrievalCandidate>(), 0));
    }
}

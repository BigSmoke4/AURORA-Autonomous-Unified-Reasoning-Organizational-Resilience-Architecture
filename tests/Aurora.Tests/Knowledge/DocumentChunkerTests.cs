using Aurora.Modules.AI.Application;
using Xunit;

namespace Aurora.Tests.Knowledge;

public sealed class DocumentChunkerTests
{
    [Fact]
    public void ShortParagraphsStayTogetherInOneChunk()
    {
        var chunks = DocumentChunker.Split("First paragraph.\n\nSecond paragraph.");

        Assert.Equal(new[] { "First paragraph.\n\nSecond paragraph." }, chunks);
    }

    [Fact]
    public void ParagraphsThatDoNotFitStartANewChunk()
    {
        var first = new string('a', 500);
        var second = new string('b', 500);

        var chunks = DocumentChunker.Split(first + "\n\n" + second);

        Assert.Equal(new[] { first, second }, chunks);
    }

    [Fact]
    public void LongParagraphIsCutAtTheLastSpaceBeforeTheLimit()
    {
        var words = Enumerable.Range(0, 400).Select(i => $"w{i:D3}");
        var text = string.Join(' ', words);

        var chunks = DocumentChunker.Split(text);

        Assert.All(chunks, chunk => Assert.True(chunk.Length <= DocumentChunker.MaxChunkLength));
        Assert.Equal(words, string.Join(' ', chunks).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void WhitespaceOnlyTextHasNoChunks()
    {
        Assert.Empty(DocumentChunker.Split(" \n\n \r\n "));
    }

    [Fact]
    public void SameTextGivesTheSameChunks()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Paragraph {i} with some words in it."));

        Assert.Equal(DocumentChunker.Split(text), DocumentChunker.Split(text));
    }
}

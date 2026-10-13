using System.Text;

namespace Aurora.Modules.AI.Application;

/// <summary>
/// Splits document text into chunks of at most <see cref="MaxChunkLength"/> characters. Paragraphs (separated by blank
/// lines) are kept together when they fit. A longer paragraph is cut at the last space before the limit, or hard-cut if
/// it has none. The split is deterministic, so re-indexing the same text gives the same chunks.
/// </summary>
public static class DocumentChunker
{
    public const int MaxChunkLength = 800;

    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(Pieces)
            .ToList();

        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            if (current.Length > 0 && current.Length + 2 + paragraph.Length > MaxChunkLength)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0) current.Append("\n\n");
            current.Append(paragraph);
        }

        if (current.Length > 0) chunks.Add(current.ToString());
        return chunks;
    }

    private static IEnumerable<string> Pieces(string paragraph)
    {
        var rest = paragraph.Replace('\n', ' ');
        while (rest.Length > MaxChunkLength)
        {
            var cut = rest.LastIndexOf(' ', MaxChunkLength);
            if (cut <= 0) cut = MaxChunkLength;
            yield return rest[..cut].Trim();
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0) yield return rest;
    }
}

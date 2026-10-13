namespace Aurora.Modules.AI.Application;

public sealed record RetrievalCandidate(Guid ChunkId, Guid DocumentId, string DocumentTitle, int Ordinal, string Text);

public sealed record RetrievalHit(Guid ChunkId, Guid DocumentId, string DocumentTitle, int Ordinal, double Score, string Snippet);

/// <summary>
/// Keyword retrieval over chunks with Okapi BM25. It is deterministic and needs no model, so the same query on the same
/// chunks always returns the same ranking. Scores are rounded to four places so that equal scores tie predictably.
/// Ties are broken by document title, then by chunk ordinal.
/// </summary>
public static class DocumentRetriever
{
    public const int MaxResults = 5;
    public const int SnippetLength = 240;
    public const int MinimumTokenLength = 2;
    private const double K1 = 1.5;
    private const double B = 0.75;
    private const int ScoreDecimals = 4;

    public static IReadOnlyList<RetrievalHit> Search(string query, IReadOnlyList<RetrievalCandidate> chunks, int limit = MaxResults)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var queryTerms = Tokenize(query).Distinct(StringComparer.Ordinal).ToList();
        if (queryTerms.Count == 0 || chunks.Count == 0) return [];

        var tokenized = chunks.Select(c => Tokenize(c.Text).ToList()).ToList();
        var averageLength = tokenized.Average(t => (double)t.Count);
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in queryTerms)
        {
            documentFrequency[term] = tokenized.Count(t => t.Contains(term, StringComparer.Ordinal));
        }

        var hits = new List<RetrievalHit>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var score = 0.0;
            foreach (var term in queryTerms)
            {
                var frequency = tokenized[i].Count(t => string.Equals(t, term, StringComparison.Ordinal));
                if (frequency == 0) continue;

                var df = documentFrequency[term];
                var idf = Math.Log(1 + (chunks.Count - df + 0.5) / (df + 0.5));
                var norm = frequency * (K1 + 1) / (frequency + K1 * (1 - B + B * tokenized[i].Count / averageLength));
                score += idf * norm;
            }

            if (score <= 0) continue;
            var chunk = chunks[i];
            hits.Add(new RetrievalHit(chunk.ChunkId, chunk.DocumentId, chunk.DocumentTitle, chunk.Ordinal,
                Math.Round(score, ScoreDecimals), Snippet(chunk.Text)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.DocumentTitle, StringComparer.Ordinal)
            .ThenBy(h => h.Ordinal)
            .Take(limit)
            .ToList();
    }

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
                continue;
            }

            AddToken(tokens, current);
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(List<string> tokens, System.Text.StringBuilder current)
    {
        if (current.Length >= MinimumTokenLength) tokens.Add(current.ToString());
        current.Clear();
    }

    private static string Snippet(string text) =>
        text.Length <= SnippetLength ? text : text[..SnippetLength].TrimEnd() + "…";
}

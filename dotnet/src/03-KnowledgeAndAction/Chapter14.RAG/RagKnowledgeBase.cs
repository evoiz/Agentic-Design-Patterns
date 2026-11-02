using System.Text.RegularExpressions;

namespace Chapter14.RAG;

public sealed record RagDocument(string Id, string Title, string Source, string Content);

public sealed record RetrievedChunk(string DocumentId, string Title, string Source, string Text, double Score);

/// <summary>
/// Tiny in-memory knowledge base with keyword-overlap retrieval. The documents are short, so each one is a
/// single chunk; a production system would chunk, embed, and index them (see Foundry File Search).
/// </summary>
public sealed class RagKnowledgeBase(Action<string>? writeSearchOutput = null)
{
    private const int TopK = 3;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "after", "also", "an", "and", "are", "as", "at", "be", "by",
        "did", "during", "for", "from", "he", "his", "how", "in", "is", "it", "of",
        "on", "or", "say", "that", "the", "to", "was", "what", "when", "where",
        "which", "who", "why", "with"
    };

    private static readonly RagDocument[] Documents =
    [
        new(
            "SOTU-2022-BREYER",
            "2022 State of the Union: Justice Breyer",
            "https://www.whitehouse.gov/state-of-the-union-2022/",
            """
            In his 2022 State of the Union address, President Joe Biden praised Justice Stephen Breyer
            for his service on the Supreme Court and announced Judge Ketanji Brown Jackson as his
            nominee to succeed Breyer. These demonstration notes paraphrase the address; consult the
            linked transcript for the original remarks.
            """),
        new(
            "SOTU-2022-ECONOMY",
            "2022 State of the Union: Economy",
            "https://www.whitehouse.gov/state-of-the-union-2022/",
            """
            President Biden's 2022 State of the Union address described economic recovery priorities,
            including creating jobs, expanding domestic manufacturing, strengthening supply chains,
            and lowering costs for families. It argued that making more goods in America and improving
            supply chains could help ease price pressures. These demonstration notes paraphrase the
            address; consult the linked transcript for the original remarks.
            """),
        new(
            "SOTU-2022-INFRASTRUCTURE",
            "2022 State of the Union: Infrastructure",
            "https://www.whitehouse.gov/state-of-the-union-2022/",
            """
            The address connected infrastructure investment with repairing roads and bridges,
            expanding broadband access, and improving transportation networks. It framed these
            projects as ways to support communities and create jobs.
            """)
    ];

    /// <summary>Scores each document by how many distinct query keywords appear in its title or content.</summary>
    public IReadOnlyList<RetrievedChunk> Retrieve(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var queryTerms = Tokenize(query).Where(term => !StopWords.Contains(term)).ToHashSet();

        return Documents
            .Select(document =>
            {
                var documentTerms = Tokenize($"{document.Title} {document.Content}").ToHashSet();
                var score = queryTerms.Count(documentTerms.Contains);
                return new RetrievedChunk(document.Id, document.Title, document.Source, document.Content.Trim(), score);
            })
            .Where(chunk => chunk.Score > 0)
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .Take(TopK)
            .ToArray();
    }

    public string Search(string query)
    {
        var matches = Retrieve(query);
        var result = matches.Count == 0
            ? $"No relevant information was found for '{query}' in the sample knowledge base."
            : string.Join(
                "\n\n",
                matches.Select(match => $"[{match.DocumentId}] {match.Title}\nSource: {match.Source}\n{match.Text}"));

        writeSearchOutput?.Invoke(result);
        return result;
    }

    private static IEnumerable<string> Tokenize(string text) =>
        Regex.Matches(text, @"[\p{L}\p{N}]+").Select(match => match.Value.ToLowerInvariant());
}

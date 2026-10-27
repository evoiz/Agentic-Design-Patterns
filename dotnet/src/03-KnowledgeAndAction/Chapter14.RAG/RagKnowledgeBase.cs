using System.Text.RegularExpressions;

namespace Chapter14.RAG;

public sealed record RagDocument(string Id, string Title, string Source, string Content);

public sealed record RetrievedChunk(string DocumentId, string Title, string Source, string Text, double Score);

public sealed class RagKnowledgeBase(Action<string>? writeSearchOutput = null)
{
    private const int ChunkSize = 500;
    private const int ChunkOverlap = 50;
    private const int DefaultTopK = 5;

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

    private readonly Action<string>? _writeSearchOutput = writeSearchOutput;

    public IReadOnlyList<RetrievedChunk> Retrieve(
        string query,
        int topK = DefaultTopK,
        double minimumScore = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumScore);

        var queryTerms = Tokenize(query)
            .Where(term => !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (queryTerms.Length == 0)
        {
            return [];
        }

        var chunks = Documents
            .SelectMany(document => SplitIntoChunks(document)
                .Select(text => new RetrievedChunk(document.Id, document.Title, document.Source, text, 0)))
            .ToArray();
        var documentFrequency = queryTerms.ToDictionary(
            term => term,
            term => chunks.Count(chunk => Tokenize(chunk.Text).Contains(term, StringComparer.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
        var averageLength = chunks.Average(chunk => Tokenize(chunk.Text).Length);

        return chunks
            .Select(chunk => chunk with { Score = Score(chunk.Text, queryTerms, documentFrequency, chunks.Length, averageLength) })
            .Where(chunk => chunk.Score > minimumScore)
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.DocumentId, StringComparer.Ordinal)
            .Take(topK)
            .ToArray();
    }

    public string Search(string query)
    {
        var matches = Retrieve(query);
        var result = matches.Count == 0
            ? $"No relevant information was found for '{query}' in the sample knowledge base."
            : string.Join(
                "\n\n",
                matches.Select(match =>
                    $"[{match.DocumentId}] {match.Title}\nSource: {match.Source}\n{match.Text.Trim()}"));

        _writeSearchOutput?.Invoke(result);
        return result;
    }

    private static double Score(
        string text,
        IReadOnlyCollection<string> queryTerms,
        IReadOnlyDictionary<string, int> documentFrequency,
        int documentCount,
        double averageLength)
    {
        var terms = Tokenize(text);
        var frequencies = terms
            .GroupBy(term => term, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var lengthNormalization = 0.5 + 0.5 * terms.Length / averageLength;

        return queryTerms.Sum(term =>
        {
            if (!frequencies.TryGetValue(term, out var frequency))
            {
                return 0;
            }

            var inverseFrequency = Math.Log(1 + (documentCount - documentFrequency[term] + 0.5) /
                (documentFrequency[term] + 0.5));
            return inverseFrequency * frequency / lengthNormalization;
        });
    }

    private static IEnumerable<string> SplitIntoChunks(RagDocument document)
    {
        var content = $"{document.Title}\n{document.Content}".Trim();
        var start = 0;
        while (start < content.Length)
        {
            var length = Math.Min(ChunkSize, content.Length - start);
            var end = start + length;
            if (end < content.Length)
            {
                var boundary = content.LastIndexOf(' ', end - 1, length);
                if (boundary > start)
                {
                    end = boundary;
                }
            }

            yield return content[start..end].Trim();
            if (end >= content.Length)
            {
                yield break;
            }

            start = Math.Max(start + 1, end - ChunkOverlap);
        }
    }

    private static string[] Tokenize(string text) =>
        Regex.Matches(text, @"[\p{L}\p{N}]+")
            .Select(match => match.Value.ToLowerInvariant())
            .ToArray();
}

using System.Text.Json;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class SimpleVectorSearchService : IVectorSearchService
{
    private readonly Dictionary<Guid, VectorItem> _vectors = new();

    public Task<bool> StoreVectorAsync(Guid id, string text, Dictionary<string, object>? metadata = null)
    {
        var item = new VectorItem
        {
            Id = id,
            Text = text,
            Metadata = metadata ?? new Dictionary<string, object>(),
            StoredAt = DateTime.UtcNow
        };

        _vectors[id] = item;
        return Task.FromResult(true);
    }

    public Task<List<VectorSearchResult>> SearchAsync(string query, int limit = 10, double threshold = 0.7)
    {
        var results = new List<VectorSearchResult>();
        
        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var vector in _vectors.Values)
        {
            var score = CalculateSimpleScore(queryWords, vector.Text.ToLowerInvariant());
            
            if (score >= threshold)
            {
                results.Add(new VectorSearchResult
                {
                    Id = vector.Id,
                    Text = vector.Text,
                    Score = score,
                    Metadata = vector.Metadata
                });
            }
        }

        return Task.FromResult(results
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList());
    }

    public Task<bool> DeleteVectorAsync(Guid id)
    {
        return Task.FromResult(_vectors.Remove(id));
    }

    private static double CalculateSimpleScore(string[] queryWords, string text)
    {
        var textWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = 0;

        foreach (var queryWord in queryWords)
        {
            if (textWords.Any(tw => tw.Contains(queryWord) || queryWord.Contains(tw)))
            {
                matches++;
            }
        }

        return queryWords.Length > 0 ? (double)matches / queryWords.Length : 0.0;
    }

    private class VectorItem
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime StoredAt { get; set; }
    }
}

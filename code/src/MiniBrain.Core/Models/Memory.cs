using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public class Memory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public required string ConversationId { get; set; }
    
    [Required]
    public required string SessionId { get; set; }
    
    [Required]
    public required string Content { get; set; }
    
    [Required]
    public required string Role { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public float[]? Embedding { get; set; }
    
    public int? ChunkIndex { get; set; }
    
    public int? TotalChunks { get; set; }
    
    public string? ParentMemoryId { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public double ImportanceScore { get; set; } = 0.5;
    
    public DateTime? ExpiryDate { get; set; }
    
    public bool IsArchived { get; set; } = false;
}



public class MemorySearchRequest
{
    public string Query { get; set; } = string.Empty;
    
    public int Limit { get; set; } = 10;
    
    public float SimilarityThreshold { get; set; } = 0.8f;
    
    public string? ConversationId { get; set; }
    
    public string? SessionId { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public DateTime? StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    public List<string> Roles { get; set; } = new();
    
    public Dictionary<string, object> MetadataFilters { get; set; } = new();
    
    public bool IncludeArchived { get; set; } = false;
    
    public double? MinImportanceScore { get; set; }
    
    public MemorySearchSortBy SortBy { get; set; } = MemorySearchSortBy.Relevance;
    
    public bool SortDescending { get; set; } = true;
}

public class MemorySearchResult
{
    public Memory Memory { get; set; } = new()
    {
        ConversationId = string.Empty,
        SessionId = string.Empty,
        Content = string.Empty,
        Role = string.Empty
    };
    
    public float SimilarityScore { get; set; }
    
    public List<string> MatchingTerms { get; set; } = new();
    
    public string? HighlightedContent { get; set; }
    
    public Dictionary<string, object> SearchMetadata { get; set; } = new();
}

public enum MemorySearchSortBy
{
    Relevance,
    Timestamp,
    ImportanceScore,
    ChunkIndex
}

public class MemoryStatistics
{
    public int TotalMemories { get; set; }
    
    public int ActiveConversations { get; set; }
    
    public int ArchivedMemories { get; set; }
    
    public DateTime OldestMemory { get; set; }
    
    public DateTime NewestMemory { get; set; }
    
    public double AverageImportanceScore { get; set; }
    
    public Dictionary<string, int> MemoryCountByRole { get; set; } = new();
    
    public Dictionary<string, int> MemoryCountByTag { get; set; } = new();
    
    public long TotalStorageSize { get; set; }
    
    public double CacheHitRate { get; set; }
}

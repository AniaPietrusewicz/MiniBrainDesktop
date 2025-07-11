namespace MiniBrain.Core.Configuration;

public class ClaudeApiSettings
{
    public required string ApiKey { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
    public int TimeoutSeconds { get; set; }
}

public class QdrantSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public int VectorSize { get; set; }
    public string Distance { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool UseHttps { get; set; } = false;
    public bool Enabled { get; set; } = true;
}

public class MemoryServiceSettings
{
    public int MaxSearchResults { get; set; } = 20;
    public float DefaultSimilarityThreshold { get; set; } = 0.8f;
    public bool CacheEnabled { get; set; } = true;
    public int CacheTTLMinutes { get; set; } = 5;
    public int MaxChunkSize { get; set; } = 1000;
    public int ChunkOverlapSize { get; set; } = 200;
}

public class MiniBrainSettings
{
    public string DefaultAgentName { get; set; } = string.Empty;
    public int MaxConversationHistory { get; set; }
    public int DefaultRetryAttempts { get; set; }
    public int DefaultTimeoutSeconds { get; set; }
    public bool EnableVectorSearch { get; set; }
    public bool EnableGoalTracking { get; set; }
}

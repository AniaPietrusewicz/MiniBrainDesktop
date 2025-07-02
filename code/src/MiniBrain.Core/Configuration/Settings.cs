namespace MiniBrain.Core.Configuration;

public class ClaudeApiSettings
{
    public const string SectionName = "ClaudeApi";
    
    public required string ApiKey { get; set; }
    public bool ApiKeyEncoded { get; set; } = false;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public string Version { get; set; } = "2023-06-01";
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 20000;
    public double Temperature { get; set; } = 0.5;
    public int TimeoutSeconds { get; set; } = 60;
}

public class QdrantSettings
{
    public const string SectionName = "Qdrant";
    
    public string BaseUrl { get; set; } = "http://localhost:6333";
    public string CollectionName { get; set; } = "minibrain_vectors";
    public int VectorSize { get; set; } = 1536;
    public string Distance { get; set; } = "Cosine";
}

public class MiniBrainSettings
{
    public const string SectionName = "MiniBrain";
    
    public string DefaultAgentName { get; set; } = "MiniBrain Assistant";
    public int MaxConversationHistory { get; set; } = 100;
    public int DefaultRetryAttempts { get; set; } = 3;
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public bool EnableVectorSearch { get; set; } = true;
    public bool EnableGoalTracking { get; set; } = true;
}

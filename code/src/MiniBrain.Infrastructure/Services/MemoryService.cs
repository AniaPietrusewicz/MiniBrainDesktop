using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;
using Qdrant.Client.Grpc;
using QdrantRange = Qdrant.Client.Grpc.Range;

namespace MiniBrain.Infrastructure.Services;

public class MemoryService : IMemoryService
{
    private readonly IQdrantClient _qdrantClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<MemoryService> _logger;
    private readonly QdrantSettings _qdrantSettings;
    private readonly ConcurrentDictionary<string, ConversationContext> _conversationCache;
    private readonly ConcurrentDictionary<string, List<Memory>> _memoryCache;
    private readonly SemaphoreSlim _initializationSemaphore;
    private bool _isInitialized = false;

    private const string COLLECTION_NAME = "minibrain_memories";
    private const int CACHE_SIZE_LIMIT = 1000;
    private const int CACHE_TTL_MINUTES = 30;

    public MemoryService(
        IQdrantClient qdrantClient,
        IEmbeddingService embeddingService,
        ILogger<MemoryService> logger,
        IConfiguration configuration)
    {
        _qdrantClient = qdrantClient;
        _embeddingService = embeddingService;
        _logger = logger;
        _conversationCache = new ConcurrentDictionary<string, ConversationContext>();
        _memoryCache = new ConcurrentDictionary<string, List<Memory>>();
        _initializationSemaphore = new SemaphoreSlim(1, 1);
        
        var qdrantConfig = configuration.GetSection("Qdrant");
        _qdrantSettings = new QdrantSettings
        {
            BaseUrl = qdrantConfig["BaseUrl"] ?? "http://localhost:6333",
            CollectionName = qdrantConfig["CollectionName"] ?? COLLECTION_NAME,
            // ⚠️ ⚠️ ⚠️ CRITICAL WARNING - DO NOT TOUCH THIS PARAMETER! ⚠️ ⚠️ ⚠️
            // This VectorSize MUST match CustomEmbeddingService embedding dimension (512).
            // Both services load from the same Qdrant.VectorSize configuration value.
            // Any change here will break vector compatibility and cause system failure!
            // This value is synchronized with CustomEmbeddingService._embeddingDimension
            VectorSize = int.Parse(qdrantConfig["VectorSize"] ?? "512"),
            Distance = qdrantConfig["Distance"] ?? "Cosine"
        };
    }

    public async Task<string> StoreMemoryAsync(Memory memory)
    {
        try
        {
            await EnsureInitializedAsync();
            
            if (string.IsNullOrEmpty(memory.Id))
                memory.Id = Guid.NewGuid().ToString();

            var chunks = await _embeddingService.ChunkTextAsync(memory.Content);
            
            if (chunks.Count == 1)
            {
                memory.Embedding = await _embeddingService.GenerateEmbeddingAsync(memory.Content);
                memory.ChunkIndex = 0;
                memory.TotalChunks = 1;
                
                await StoreMemoryInQdrantAsync(memory);
                _logger.LogInformation("Stored single memory chunk: {Id}", memory.Id);
                
                return memory.Id;
            }
            else
            {
                var parentId = memory.Id;
                var storedIds = new List<string>();
                
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var chunkMemory = new Memory
                    {
                        Id = $"{parentId}_chunk_{i}",
                        ConversationId = memory.ConversationId,
                        SessionId = memory.SessionId,
                        Content = chunk.Content,
                        Role = memory.Role,
                        Timestamp = memory.Timestamp,
                        Metadata = new Dictionary<string, object>(memory.Metadata)
                        {
                            ["chunk_start"] = chunk.StartIndex,
                            ["chunk_end"] = chunk.EndIndex,
                            ["chunk_metadata"] = chunk.Metadata
                        },
                        ChunkIndex = i,
                        TotalChunks = chunks.Count,
                        ParentMemoryId = parentId,
                        Tags = new List<string>(memory.Tags),
                        ImportanceScore = memory.ImportanceScore,
                        ExpiryDate = memory.ExpiryDate,
                        IsArchived = memory.IsArchived
                    };
                    
                    chunkMemory.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunkMemory.Content);
                    await StoreMemoryInQdrantAsync(chunkMemory);
                    storedIds.Add(chunkMemory.Id);
                }
                
                _logger.LogInformation("Stored {Count} memory chunks for parent: {ParentId}", chunks.Count, parentId);
                return parentId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing memory: {MemoryId}", memory.Id);
            throw;
        }
    }

    public async Task<List<Memory>> RetrieveMemoriesAsync(string query, int limit = 10)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            // Follow architecture: Use filters for context-aware search
            var filter = new Filter
            {
                Must = 
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "timestamp",
                            Range = new QdrantRange
                            {
                                Gte = ((DateTimeOffset)DateTime.UtcNow.AddDays(-30)).ToUnixTimeSeconds()
                            }
                        }
                    }
                }
            };
            
            var searchResult = await _qdrantClient.SearchAsync(
                COLLECTION_NAME, 
                queryEmbedding, 
                Math.Min(limit * 2, 100)
            );
            
            var memories = searchResult.Select(ConvertToMemory).ToList();
            
            // Apply recency weighting as per architecture
            memories = ApplyRecencyWeighting(memories);
            
            var deduplicatedMemories = DeduplicateMemories(memories, limit);
            
            _logger.LogInformation("Retrieved {Count} memories for query: {Query}", deduplicatedMemories.Count, query[..Math.Min(50, query.Length)]);
            
            return deduplicatedMemories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving memories for query: {Query}", query);
            return new List<Memory>();
        }
    }

    public async Task<Conversation> GetConversationHistoryAsync(string conversationId)
    {
        try
        {
            if (_conversationCache.TryGetValue(conversationId, out var cachedConversation))
            {
                _logger.LogDebug("Retrieved conversation from cache: {ConversationId}", conversationId);
                return ConvertToConversation(cachedConversation);
            }

            await EnsureInitializedAsync();
            
            // Use simplified interface - retrieve by conversation ID strings
            var conversationIds = new List<string> { conversationId };
            var records = await _qdrantClient.RetrieveAsync(COLLECTION_NAME, conversationIds);
            
            if (!records.Any())
            {
                _logger.LogWarning("No memories found for conversation: {ConversationId}", conversationId);
                return new Conversation { Id = conversationId };
            }

            var memories = records.Select(ConvertRecordToMemory).ToList();
            memories = DeduplicateMemories(memories, memories.Count);
            
            // Create Conversation with Memory integration (following architecture)
            var conversation = new Conversation
            {
                Id = conversationId,
                SessionId = memories.FirstOrDefault()?.SessionId ?? string.Empty,
                StartTime = memories.Min(m => m.Timestamp),
                EndTime = memories.Max(m => m.Timestamp),
                Messages = memories,
                Summary = string.Empty,
                Tags = new List<string>()
            };

            CacheConversation(conversationId, ConvertFromConversation(conversation));
            
            _logger.LogInformation("Retrieved conversation history: {ConversationId} with {Count} messages", conversationId, memories.Count);
            
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation history: {ConversationId}", conversationId);
            return new Conversation { Id = conversationId };
        }
    }

    public async Task<List<Memory>> SearchSimilarMemoriesAsync(string content, float threshold = 0.8f)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(content);
            
            var searchResult = await _qdrantClient.SearchAsync(
                COLLECTION_NAME,
                queryEmbedding,
                20
            );
            
            var memories = searchResult.Select(ConvertScoredPointToMemory).ToList();
            
            // Apply relevance scoring as per architecture
            memories = ApplyRelevanceScoring(memories);
            
            memories = DeduplicateMemories(memories, memories.Count);
            
            _logger.LogInformation("Found {Count} similar memories with threshold {Threshold}", memories.Count, threshold);
            
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar memories: {Content}", content[..Math.Min(50, content.Length)]);
            return new List<Memory>();
        }
    }

    public async Task<bool> DeleteMemoryAsync(string memoryId)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var result = await _qdrantClient.DeleteAsync(COLLECTION_NAME, new List<string> { memoryId });
            
            if (result)
            {
                InvalidateCache(memoryId);
                _logger.LogInformation("Successfully deleted memory: {MemoryId}", memoryId);
            }
            else
            {
                _logger.LogWarning("Failed to delete memory: {MemoryId}", memoryId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting memory: {MemoryId}", memoryId);
            return false;
        }
    }

    private Memory ConvertRecordToMemory(Record record)
    {
        var payload = record.Payload;
        
        var memory = new Memory
        {
            Id = record.Id,
            ConversationId = payload.ContainsKey("conversation_id") ? payload["conversation_id"].ToString() ?? "" : "",
            SessionId = payload.ContainsKey("session_id") ? payload["session_id"].ToString() ?? "" : "",
            Content = payload.ContainsKey("content") ? payload["content"].ToString() ?? "" : "",
            Role = payload.ContainsKey("role") ? payload["role"].ToString() ?? "" : "",
            Timestamp = payload.ContainsKey("timestamp") ? 
                DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(payload["timestamp"])).DateTime : 
                DateTime.UtcNow,
            ImportanceScore = payload.ContainsKey("importance_score") ? Convert.ToDouble(payload["importance_score"]) : 0.5,
            IsArchived = payload.ContainsKey("is_archived") && Convert.ToBoolean(payload["is_archived"])
        };

        if (payload.ContainsKey("tags"))
        {
            try
            {
                memory.Tags = JsonSerializer.Deserialize<List<string>>(payload["tags"].ToString() ?? "[]") ?? new List<string>();
            }
            catch
            {
                memory.Tags = new List<string>();
            }
        }

        if (payload.ContainsKey("metadata"))
        {
            try
            {
                memory.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(payload["metadata"].ToString() ?? "{}") ?? new Dictionary<string, object>();
            }
            catch
            {
                memory.Metadata = new Dictionary<string, object>();
            }
        }

        return memory;
    }

    private Memory ConvertScoredPointToMemory(ScoredPoint scoredPoint)
    {
        var payload = scoredPoint.Payload;
        
        var memory = new Memory
        {
            Id = payload["id"].StringValue,
            ConversationId = payload["conversation_id"].StringValue,
            SessionId = payload["session_id"].StringValue,
            Content = payload["content"].StringValue,
            Role = payload["role"].StringValue,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)payload["timestamp"].DoubleValue).DateTime,
            ChunkIndex = payload.ContainsKey("chunk_index") ? (int)payload["chunk_index"].IntegerValue : 0,
            TotalChunks = payload.ContainsKey("total_chunks") ? (int)payload["total_chunks"].IntegerValue : 1,
            ParentMemoryId = payload.ContainsKey("parent_memory_id") ? payload["parent_memory_id"].StringValue : null,
            ImportanceScore = payload.ContainsKey("importance_score") ? payload["importance_score"].DoubleValue : 0.5,
            IsArchived = payload.ContainsKey("is_archived") && payload["is_archived"].BoolValue
        };

        if (payload.ContainsKey("tags"))
        {
            try
            {
                memory.Tags = JsonSerializer.Deserialize<List<string>>(payload["tags"].StringValue) ?? new List<string>();
            }
            catch
            {
                memory.Tags = new List<string>();
            }
        }

        if (payload.ContainsKey("metadata"))
        {
            try
            {
                memory.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(payload["metadata"].StringValue) ?? new Dictionary<string, object>();
            }
            catch
            {
                memory.Metadata = new Dictionary<string, object>();
            }
        }

        return memory;
    }

    private Conversation ConvertToConversation(ConversationContext context)
    {
        return new Conversation
        {
            Id = context.Id.ToString(),
            SessionId = context.SessionId,
            StartTime = context.CreatedAt,
            EndTime = context.EndTime ?? DateTime.UtcNow,
            Messages = new List<Memory>(),
            Summary = context.Metadata.ContainsKey("summary") ? context.Metadata["summary"].ToString() ?? "" : "",
            Tags = new List<string>()
        };
    }

    private ConversationContext ConvertFromConversation(Conversation conversation)
    {
        return new ConversationContext
        {
            Id = Guid.Parse(conversation.Id),
            SessionId = conversation.SessionId,
            CreatedAt = conversation.StartTime,
            EndTime = conversation.EndTime,
            Messages = new List<Message>(),
            IsActive = true,
            Metadata = new Dictionary<string, object>
            {
                ["summary"] = conversation.Summary,
                ["tags"] = conversation.Tags
            }
        };
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await InitializeQdrantCollectionAsync();
            _isInitialized = true;
            
            _logger.LogInformation("MemoryService initialized successfully");
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private async Task InitializeQdrantCollectionAsync()
    {
        try
        {
            // Try to create the collection - if it already exists, handle gracefully
            var config = new MiniBrain.Core.Models.CollectionConfig
            {
                Name = COLLECTION_NAME,
                VectorSize = _qdrantSettings.VectorSize,
                Distance = _qdrantSettings.Distance
            };

            await _qdrantClient.CreateCollectionAsync(config);
            _logger.LogInformation("Qdrant collection created: {CollectionName}", COLLECTION_NAME);
        }
        catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("AlreadyExists"))
        {
            _logger.LogInformation("Qdrant collection already exists: {CollectionName}", COLLECTION_NAME);
            // This is expected and not an error - collection already exists
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Qdrant collection: {CollectionName}", COLLECTION_NAME);
            throw;
        }
    }

    private async Task StoreMemoryInQdrantAsync(Memory memory)
    {
        if (memory.Embedding == null)
            throw new InvalidOperationException("Memory embedding is null");

        var payload = new Dictionary<string, Value>
        {
            ["id"] = memory.Id,
            ["conversation_id"] = memory.ConversationId,
            ["session_id"] = memory.SessionId,
            ["content"] = memory.Content,
            ["role"] = memory.Role,
            ["timestamp"] = ((DateTimeOffset)memory.Timestamp).ToUnixTimeSeconds(),
            ["chunk_index"] = memory.ChunkIndex ?? 0,
            ["total_chunks"] = memory.TotalChunks ?? 1,
            ["parent_memory_id"] = memory.ParentMemoryId ?? string.Empty,
            ["tags"] = JsonSerializer.Serialize(memory.Tags),
            ["importance_score"] = memory.ImportanceScore,
            ["is_archived"] = memory.IsArchived,
            ["metadata"] = JsonSerializer.Serialize(memory.Metadata)
        };

        if (memory.ExpiryDate.HasValue)
            payload["expiry_date"] = memory.ExpiryDate.Value.ToString("O");

        var point = new PointStruct
        {
            Id = new PointId { Uuid = memory.Id },
            Vectors = new Vectors { Vector = memory.Embedding },
            Payload = { payload }
        };

        await _qdrantClient.UpsertAsync(COLLECTION_NAME, new List<PointStruct> { point });
    }

    private Memory ConvertToMemory(RetrievedPoint point)
    {
        var payload = point.Payload;
        
        var memory = new Memory
        {
            Id = payload["id"].StringValue,
            ConversationId = payload["conversation_id"].StringValue,
            SessionId = payload["session_id"].StringValue,
            Content = payload["content"].StringValue,
            Role = payload["role"].StringValue,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)payload["timestamp"].DoubleValue).DateTime,
            ChunkIndex = payload.ContainsKey("chunk_index") ? (int)payload["chunk_index"].IntegerValue : 0,
            TotalChunks = payload.ContainsKey("total_chunks") ? (int)payload["total_chunks"].IntegerValue : 1,
            ParentMemoryId = payload.ContainsKey("parent_memory_id") ? payload["parent_memory_id"].StringValue : null,
            ImportanceScore = payload.ContainsKey("importance_score") ? payload["importance_score"].DoubleValue : 0.5,
            IsArchived = payload.ContainsKey("is_archived") && payload["is_archived"].BoolValue
        };

        if (payload.ContainsKey("tags"))
        {
            try
            {
                memory.Tags = JsonSerializer.Deserialize<List<string>>(payload["tags"].StringValue) ?? new List<string>();
            }
            catch
            {
                memory.Tags = new List<string>();
            }
        }

        if (payload.ContainsKey("metadata"))
        {
            try
            {
                memory.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(payload["metadata"].StringValue) ?? new Dictionary<string, object>();
            }
            catch
            {
                memory.Metadata = new Dictionary<string, object>();
            }
        }

        if (payload.ContainsKey("expiry_date"))
        {
            if (DateTime.TryParse(payload["expiry_date"].StringValue, out var expiryDate))
                memory.ExpiryDate = expiryDate;
        }

        return memory;
    }

    private Memory ConvertToMemory(ScoredPoint scoredPoint)
    {
        var payload = scoredPoint.Payload;
        
        var memory = new Memory
        {
            Id = payload["id"].StringValue,
            ConversationId = payload["conversation_id"].StringValue,
            SessionId = payload["session_id"].StringValue,
            Content = payload["content"].StringValue,
            Role = payload["role"].StringValue,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)payload["timestamp"].DoubleValue).DateTime,
            ChunkIndex = payload.ContainsKey("chunk_index") ? (int)payload["chunk_index"].IntegerValue : 0,
            TotalChunks = payload.ContainsKey("total_chunks") ? (int)payload["total_chunks"].IntegerValue : 1,
            ParentMemoryId = payload.ContainsKey("parent_memory_id") ? payload["parent_memory_id"].StringValue : null,
            ImportanceScore = payload.ContainsKey("importance_score") ? payload["importance_score"].DoubleValue : 0.5,
            IsArchived = payload.ContainsKey("is_archived") && payload["is_archived"].BoolValue
        };

        if (payload.ContainsKey("tags"))
        {
            try
            {
                memory.Tags = JsonSerializer.Deserialize<List<string>>(payload["tags"].StringValue) ?? new List<string>();
            }
            catch
            {
                memory.Tags = new List<string>();
            }
        }

        if (payload.ContainsKey("metadata"))
        {
            try
            {
                memory.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(payload["metadata"].StringValue) ?? new Dictionary<string, object>();
            }
            catch
            {
                memory.Metadata = new Dictionary<string, object>();
            }
        }

        if (payload.ContainsKey("expiry_date"))
        {
            if (DateTime.TryParse(payload["expiry_date"].StringValue, out var expiryDate))
                memory.ExpiryDate = expiryDate;
        }

        return memory;
    }

    private List<Memory> DeduplicateMemories(List<Memory> memories, int limit)
    {
        var parentGroups = memories.GroupBy(m => m.ParentMemoryId ?? m.Id).ToList();
        var deduplicated = new List<Memory>();

        foreach (var group in parentGroups)
        {
            if (group.Count() == 1)
            {
                deduplicated.Add(group.First());
            }
            else
            {
                var orderedChunks = group.OrderBy(m => m.ChunkIndex).ToList();
                var combinedContent = string.Join(" ", orderedChunks.Select(m => m.Content));
                
                var masterMemory = orderedChunks.First();
                masterMemory.Content = combinedContent;
                masterMemory.ChunkIndex = 0;
                masterMemory.TotalChunks = 1;
                masterMemory.ParentMemoryId = null;
                
                deduplicated.Add(masterMemory);
            }
        }

        return deduplicated.Take(limit).ToList();
    }

    private void CacheConversation(string conversationId, ConversationContext conversation)
    {
        if (_conversationCache.Count >= CACHE_SIZE_LIMIT)
        {
            var oldestKey = _conversationCache.Keys.FirstOrDefault();
            if (oldestKey != null)
                _conversationCache.TryRemove(oldestKey, out _);
        }

        _conversationCache.TryAdd(conversationId, conversation);
    }

    private void InvalidateCache(string memoryId)
    {
        // For ConversationContext, we need to invalidate based on conversation ID
        // since Messages are not Memory objects but Message objects
        var conversationsToRemove = _conversationCache.Keys.ToList();
        
        foreach (var conversationId in conversationsToRemove)
        {
            _conversationCache.TryRemove(conversationId, out _);
        }

        var memoriesToRemove = _memoryCache
            .Where(kvp => kvp.Value.Any(m => m.Id == memoryId || m.ParentMemoryId == memoryId))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in memoriesToRemove)
        {
            _memoryCache.TryRemove(key, out _);
        }
    }

    // Ranking Algorithms Implementation (as per architecture)
    
    private List<Memory> ApplyRecencyWeighting(List<Memory> memories)
    {
        // Apply recency weighting: more recent memories get higher scores
        var now = DateTime.UtcNow;
        
        return memories
            .Select(m => 
            {
                var daysSinceCreation = (now - m.Timestamp).TotalDays;
                var recencyScore = Math.Max(0, 1.0 - (daysSinceCreation / 30.0)); // Weight over 30 days
                
                // Adjust importance score based on recency
                m.ImportanceScore = (m.ImportanceScore * 0.7) + (recencyScore * 0.3);
                
                return m;
            })
            .OrderByDescending(m => m.ImportanceScore)
            .ToList();
    }
    
    private List<Memory> ApplyRelevanceScoring(List<Memory> memories)
    {
        // Apply relevance scoring: combine cosine similarity with content relevance
        return memories
            .Select(m => 
            {
                // Boost importance based on role (assistant messages often more important)
                var roleBoost = m.Role.ToLower() == "assistant" ? 0.1 : 0.0;
                
                // Boost based on content length (more detailed responses often more important)
                var lengthBoost = Math.Min(0.1, m.Content.Length / 1000.0 * 0.1);
                
                m.ImportanceScore += roleBoost + lengthBoost;
                
                return m;
            })
            .OrderByDescending(m => m.ImportanceScore)
            .ToList();
    }

    // =================== TEMPORAL SEARCH IMPLEMENTATION ===================
    // Search by time ranges and date filters (as per architecture)
    
    public async Task<List<Memory>> SearchMemoriesByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 10)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var filter = new Filter
            {
                Must = 
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "timestamp",
                            Range = new QdrantRange
                            {
                                Gte = ((DateTimeOffset)startTime).ToUnixTimeSeconds(),
                                Lte = ((DateTimeOffset)endTime).ToUnixTimeSeconds()
                            }
                        }
                    }
                }
            };
            
            // Use a neutral query vector for temporal-only search
            var neutralEmbedding = new float[_qdrantSettings.VectorSize];
            
            var searchResult = await _qdrantClient.SearchAsync(
                COLLECTION_NAME,
                neutralEmbedding,
                limit
            );
            
            var memories = searchResult.Select(ConvertToMemory).ToList();
            
            // Apply temporal sorting (most recent first)
            memories = memories.OrderByDescending(m => m.Timestamp).ToList();
            
            _logger.LogInformation("Found {Count} memories in time range {Start} to {End}", 
                memories.Count, startTime, endTime);
            
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching memories by time range: {Start} to {End}", startTime, endTime);
            return new List<Memory>();
        }
    }
    
    public async Task<List<Memory>> SearchRecentMemoriesAsync(TimeSpan timeSpan, int limit = 10)
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.Subtract(timeSpan);
        
        return await SearchMemoriesByTimeRangeAsync(startTime, endTime, limit);
    }
    
    // =================== HYBRID SEARCH IMPLEMENTATION ===================
    // Combines semantic + temporal + context criteria (as per architecture)
    
    public async Task<List<Memory>> HybridSearchAsync(string query, DateTime? startTime = null, DateTime? endTime = null, 
                                                     string? conversationId = null, float threshold = 0.8f, int limit = 10)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            // Build complex filter combining multiple criteria
            var conditions = new List<Condition>();
            
            // Add temporal filter if specified
            if (startTime.HasValue || endTime.HasValue)
            {
                var range = new QdrantRange();
                if (startTime.HasValue)
                    range.Gte = ((DateTimeOffset)startTime.Value).ToUnixTimeSeconds();
                if (endTime.HasValue)
                    range.Lte = ((DateTimeOffset)endTime.Value).ToUnixTimeSeconds();
                
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "timestamp",
                        Range = range
                    }
                });
            }
            
            // Add conversation filter if specified
            if (!string.IsNullOrEmpty(conversationId))
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "conversation_id",
                        Match = new Match { Keyword = conversationId }
                    }
                });
            }
            
            // Add importance threshold filter
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "importance_score",
                    Range = new QdrantRange { Gte = threshold * 0.5 } // Lower threshold for hybrid search
                }
            });
            
            var filter = conditions.Any() ? new Filter { Must = { conditions } } : null;
            
            var searchResult = await _qdrantClient.SearchAsync(
                COLLECTION_NAME,
                queryEmbedding,
                Math.Min(limit * 3, 100) // Get more results for better ranking
            );
            
            var memories = searchResult.Select(ConvertToMemory).ToList();
            
            // Apply hybrid ranking: combine semantic similarity + recency + relevance
            memories = ApplyHybridRanking(memories, query);
            
            var finalResults = memories.Take(limit).ToList();
            
            _logger.LogInformation("Hybrid search found {Count} memories for query: {Query}", 
                finalResults.Count, query[..Math.Min(50, query.Length)]);
            
            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hybrid search for query: {Query}", query);
            return new List<Memory>();
        }
    }
    
    // =================== ENHANCED CONTEXT-AWARE SEARCH ===================
    // More sophisticated context understanding (as per architecture)
    
    public async Task<List<Memory>> ContextAwareSearchAsync(string query, string currentConversationId, 
                                                           string sessionId, int limit = 10)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            // Build context-aware filter with weighted priorities
            var conditions = new List<Condition>();
            
            // Priority 1: Current session (highest weight)
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "session_id",
                    Match = new Match { Keyword = sessionId }
                }
            });
            
            // Priority 2: Recent timeframe (last 7 days)
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "timestamp",
                    Range = new QdrantRange
                    {
                        Gte = ((DateTimeOffset)DateTime.UtcNow.AddDays(-7)).ToUnixTimeSeconds()
                    }
                }
            });
            
            // Priority 3: High importance memories
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "importance_score",
                    Range = new QdrantRange { Gte = 0.6 }
                }
            });
            
            var filter = new Filter { Should = { conditions } }; // Use 'Should' for OR logic
            
            var searchResult = await _qdrantClient.SearchAsync(
                COLLECTION_NAME,
                queryEmbedding,
                Math.Min(limit * 2, 50)
            );
            
            var memories = searchResult.Select(ConvertToMemory).ToList();
            
            // Apply context-aware ranking
            memories = ApplyContextAwareRanking(memories, currentConversationId, sessionId);
            
            var finalResults = DeduplicateMemories(memories, limit);
            
            _logger.LogInformation("Context-aware search found {Count} memories for conversation: {ConversationId}", 
                finalResults.Count, currentConversationId);
            
            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in context-aware search for conversation: {ConversationId}", currentConversationId);
            return new List<Memory>();
        }
    }

    public void Dispose()
    {
        if (_qdrantClient is IDisposable disposableClient)
        {
            disposableClient.Dispose();
        }
        _initializationSemaphore?.Dispose();
    }

    // =================== ENHANCED RANKING ALGORITHMS ===================
    // Supporting the new search strategies (as per architecture)
    
    private List<Memory> ApplyHybridRanking(List<Memory> memories, string query)
    {
        var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        return memories
            .Select(m => 
            {
                var score = m.ImportanceScore;
                
                // Semantic relevance (already in base score)
                // Keyword boost for exact matches
                var contentLower = m.Content.ToLower();
                var keywordMatches = queryWords.Count(word => contentLower.Contains(word));
                var keywordBoost = (keywordMatches / (double)queryWords.Length) * 0.2;
                
                // Recency boost (within last 24 hours gets extra weight)
                var hoursSinceCreation = (DateTime.UtcNow - m.Timestamp).TotalHours;
                var recencyBoost = hoursSinceCreation < 24 ? 0.15 : 0.0;
                
                // Role boost (assistant responses often more valuable)
                var roleBoost = m.Role.ToLower() == "assistant" ? 0.1 : 0.0;
                
                // Content quality boost (longer, detailed responses)
                var qualityBoost = Math.Min(0.1, m.Content.Length / 2000.0 * 0.1);
                
                // Combine all factors
                m.ImportanceScore = score + keywordBoost + recencyBoost + roleBoost + qualityBoost;
                
                return m;
            })
            .OrderByDescending(m => m.ImportanceScore)
            .ToList();
    }
    
    private List<Memory> ApplyContextAwareRanking(List<Memory> memories, string currentConversationId, string sessionId)
    {
        return memories
            .Select(m => 
            {
                var score = m.ImportanceScore;
                
                // Session relevance (same session gets highest boost)
                var sessionBoost = m.SessionId == sessionId ? 0.3 : 0.0;
                
                // Conversation continuity (same conversation gets boost)
                var conversationBoost = m.ConversationId == currentConversationId ? 0.2 : 0.0;
                
                // Temporal proximity (recent within session)
                var hoursSinceCreation = (DateTime.UtcNow - m.Timestamp).TotalHours;
                var proximityBoost = hoursSinceCreation < 1 ? 0.25 : 
                                   hoursSinceCreation < 6 ? 0.15 : 
                                   hoursSinceCreation < 24 ? 0.05 : 0.0;
                
                // Content type boost (questions often need follow-up context)
                var contentTypeBoost = m.Content.Contains("?") ? 0.1 : 0.0;
                
                // Combine context factors
                m.ImportanceScore = score + sessionBoost + conversationBoost + proximityBoost + contentTypeBoost;
                
                return m;
            })
            .OrderByDescending(m => m.ImportanceScore)
            .ToList();
    }
}

# Claude Memory & Vector Search Investigation

## Request (2025-07-02 18:03)
User asked how Claude accesses vector search context and remembers shit from previous messages.

## Investigation Results

### 🧠 How Claude Gets Memory Context

#### Two-Tier Memory System

**1. Conversation History (Recent Messages)**
- **Source**: SQL Server LocalDB via Entity Framework
- **Location**: `ConversationService.GetConversationHistoryAsync()`
- **Limit**: Last 20 messages from current session
- **Method**: Database query ordered by timestamp

**2. Vector Search Context (Semantic Memory)**
- **Source**: Qdrant Vector Database  
- **Location**: `ConversationService.GetRelevantContextAsync()`
- **Limit**: Top 5 most semantically similar results
- **Threshold**: 0.7 similarity score
- **Method**: Vector embedding search across ALL stored messages

### 📝 The Memory Flow Process

When you send a message, here's the exact fucking sequence:

1. **Message Storage**: Your message gets saved to SQL + vectorized to Qdrant
2. **History Retrieval**: Gets last 20 messages from current session
3. **Semantic Search**: Searches ALL past messages for semantic relevance
4. **System Prompt Building**: Combines agent instructions + context + capabilities
5. **Claude Request**: Sends history + system prompt to Claude API

### 🔍 Vector Search Implementation Details

**StoreVectorAsync Process:**
```csharp
await _vectorSearchService.StoreVectorAsync(message.Id, content, new Dictionary<string, object>
{
    ["type"] = "message",
    ["role"] = role,
    ["session_id"] = sessionId,
    ["timestamp"] = message.Timestamp
});
```

**SearchAsync Process:**
```csharp
var relevantContext = await _vectorSearchService.SearchAsync(userMessage, 5, 0.7);
```

**System Prompt Construction:**
```csharp
var systemPrompt = agent.Instructions;

if (context.Any())
{
    systemPrompt += "\n\nRelevant context from previous interactions:\n";
    foreach (var item in context)
    {
        systemPrompt += $"- {item.Text}\n";
    }
}
```

### 🎯 What Claude Actually Receives

**Recent Messages Array**: Last 20 messages from current session
**System Prompt Contains**:
- Agent's specific instructions
- Relevant context from vector search (cross-session!)
- Current capabilities list
- Metadata about message roles and timestamps

### 💾 Storage Locations

**Recent Message Memory**:
- **Database**: SQL Server LocalDB
- **Table**: Messages with ConversationContext relationship
- **Scope**: Session-specific (current conversation only)

**Semantic Memory**:
- **Database**: Qdrant Vector Database (port 6333)
- **Collection**: minibrain_vectors
- **Scope**: Global (searches across ALL conversations ever)

### 🔥 Key Insights

1. **Claude doesn't directly access databases** - everything comes through the system prompt
2. **Two memory types**: Recent (session-specific) + Semantic (global search)
3. **Vector search is cross-session** - can find relevant shit from months ago
4. **System prompt is rebuilt every time** with fresh context
5. **Memory is append-only** - nothing gets deleted, just searched

### ⚡ Performance Notes

**Vector Search Fallback**:
```csharp
private async Task<List<VectorSearchResult>> GetRelevantContextAsync(string query)
{
    try
    {
        return await _vectorSearchService.SearchAsync(query, 5, 0.7);
    }
    catch
    {
        return new List<VectorSearchResult>(); // Graceful degradation
    }
}
```

If Qdrant is down, Claude still gets recent messages but loses semantic context.

## Configuration

**Vector Settings** (appsettings.json):
- VectorSize: 1536 (OpenAI embedding dimensions)
- Distance: Cosine similarity
- EnableVectorSearch: true

**Memory Limits**:
- Recent messages: 20 per session
- Vector results: 5 most relevant
- Similarity threshold: 0.7 (pretty strict)

## Conclusion

Claude's memory is fucking sophisticated - he gets both short-term (recent messages) and long-term (semantic search) context automatically injected into every conversation via the system prompt. The vector search means he can remember relevant shit from completely different conversations if it's semantically related to your current question.

**Memory is NOT direct database access** - it's all pre-processed and fed to Claude as context strings in the system prompt.

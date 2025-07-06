using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly IMiniBrainDbContext _context;
    private readonly IClaudeApiService _claudeService;
    private readonly IMemoryService _memoryService;

    public ConversationService(
        IMiniBrainDbContext context, 
        IClaudeApiService claudeService,
        IMemoryService memoryService)
    {
        _context = context;
        _claudeService = claudeService;
        _memoryService = memoryService;
    }

    public async Task<ConversationContext> CreateConversationAsync(Guid agentId, string sessionId)
    {
        var existingConversation = await _context.ConversationContexts
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);
        
        if (existingConversation != null)
        {
            existingConversation.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existingConversation;
        }

        var conversation = new ConversationContext
        {
            AgentId = agentId,
            SessionId = sessionId
        };

        _context.ConversationContexts.Add(conversation);
        
        try
        {
            await _context.SaveChangesAsync();
            return conversation;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_ConversationContexts_SessionId") == true)
        {
            // Handle race condition where another request created the same conversation
            // Detach the failed conversation entity if context is DbContext
            if (_context is DbContext dbContext)
            {
                dbContext.Entry(conversation).State = EntityState.Detached;
            }
            
            existingConversation = await _context.ConversationContexts
                .FirstOrDefaultAsync(c => c.SessionId == sessionId);
                
            if (existingConversation != null)
            {
                existingConversation.LastAccessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return existingConversation;
            }
            
            // If we still can't find it, rethrow the original exception
            throw;
        }
    }

    public async Task<ConversationContext?> GetConversationAsync(string sessionId)
    {
        var conversation = await _context.ConversationContexts
            .Include(c => c.Agent)
            .Include(c => c.Messages.OrderBy(m => m.Timestamp))
            .FirstOrDefaultAsync(c => c.SessionId == sessionId);

        if (conversation != null)
        {
            conversation.LastAccessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return conversation;
    }

    public async Task<Message> AddMessageAsync(string sessionId, string role, string content)
    {
        var conversation = await GetConversationAsync(sessionId);
        if (conversation == null)
            throw new ArgumentException($"Conversation {sessionId} not found");

        var message = new Message
        {
            ConversationContextId = conversation.Id,
            Role = role,
            Content = content
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Create Memory object and store via MemoryService (following architecture)
        var memory = new Memory
        {
            Id = message.Id.ToString(),
            ConversationId = conversation.Id.ToString(),
            SessionId = sessionId,
            Content = content,
            Role = role,
            Timestamp = message.Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["message_id"] = message.Id,
                ["conversation_context_id"] = conversation.Id,
                ["agent_id"] = conversation.AgentId
            },
            Tags = new List<string>(),
            ImportanceScore = 0.5,
            IsArchived = false
        };

        // Follow architecture: ConversationService -> MemoryService -> EmbeddingService -> Qdrant
        await _memoryService.StoreMemoryAsync(memory);

        return message;
    }

    public async Task<List<Message>> GetConversationHistoryAsync(string sessionId, int limit = 50)
    {
        return await _context.Messages
            .Include(m => m.ConversationContext)
            .Where(m => m.ConversationContext.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<string> ProcessMessageAsync(string sessionId, string userMessage)
    {
        var conversation = await GetConversationAsync(sessionId);
        if (conversation == null)
            throw new ArgumentException($"Conversation {sessionId} not found");

        await AddMessageAsync(sessionId, "user", userMessage);

        var recentMessages = await GetConversationHistoryAsync(sessionId, 20);
        
        var relevantContext = await GetRelevantContextAsync(userMessage, sessionId);
        
        var systemPrompt = BuildSystemPrompt(conversation.Agent, relevantContext);
        
        // Use tools-enabled API call to give Claude access to web browsing and other tools
        var response = await _claudeService.SendMessagesWithToolsAsync(recentMessages, systemPrompt);

        await AddMessageAsync(sessionId, "assistant", response);

        return response;
    }

    public async Task<string> ProcessMessageAsync(string sessionId, string userMessage, Guid? agentId = null)
    {
        // Special handling for direct Claude API calls (no agent system)
        var directClaudeAgentId = new Guid("00000000-0000-0000-0000-000000000001");
        
        var conversation = await GetConversationAsync(sessionId);
        if (conversation == null)
        {
            if (agentId == null)
                throw new ArgumentException($"Conversation {sessionId} not found and no AgentId provided to create it");
            
            // For direct Claude calls, create a minimal conversation without agent dependency
            if (agentId == directClaudeAgentId)
            {
                conversation = await CreateDirectClaudeConversationAsync(sessionId);
            }
            else
            {
                conversation = await CreateConversationAsync(agentId.Value, sessionId);
            }
        }

        await AddMessageAsync(sessionId, "user", userMessage);

        var recentMessages = await GetConversationHistoryAsync(sessionId, 20);
        
        var relevantContext = await GetRelevantContextAsync(userMessage, sessionId);
        
        // Use simple system prompt for direct Claude calls
        string systemPrompt;
        if (agentId == directClaudeAgentId || conversation.Agent == null)
        {
            systemPrompt = BuildDirectClaudeSystemPrompt(relevantContext);
        }
        else
        {
            systemPrompt = BuildSystemPrompt(conversation.Agent, relevantContext);
        }
        
        // Use tools-enabled API call to give Claude access to web browsing and other tools
        var response = await _claudeService.SendMessagesWithToolsAsync(recentMessages, systemPrompt);

        await AddMessageAsync(sessionId, "assistant", response);

        return response;
    }

    private async Task<List<Memory>> GetRelevantContextAsync(string query, string sessionId)
    {
        try
        {
            // For now, use a smart default strategy combining context-aware and recent
            // In the future, this could be enhanced with AI decision-making
            var contextAwareResults = await _memoryService.ContextAwareSearchAsync(query, sessionId, sessionId, 3);
            var recentResults = await _memoryService.SearchRecentMemoriesAsync(TimeSpan.FromHours(24), 3);
            
            // Combine and deduplicate results
            var allResults = new List<Memory>();
            allResults.AddRange(contextAwareResults);
            allResults.AddRange(recentResults.Where(r => !allResults.Any(a => a.Id == r.Id)));
            
            return allResults.Take(5).ToList();
        }
        catch
        {
            // Fallback to basic semantic search
            try
            {
                return await _memoryService.RetrieveMemoriesAsync(query, 5);
            }
            catch
            {
                return new List<Memory>();
            }
        }
    }

    private string BuildSystemPrompt(Agent agent, List<Memory> context)
    {
        var systemPrompt = agent.Instructions;

        if (context.Any())
        {
            systemPrompt += "\n\nRelevant context from previous interactions:\n";
            foreach (var memory in context)
            {
                systemPrompt += $"- [{memory.Role}] {memory.Content}\n";
            }
        }

        systemPrompt += "\n\nCurrent capabilities:\n";
        systemPrompt += "- Goal tracking and management\n";
        systemPrompt += "- Workflow execution\n";
        systemPrompt += "- Context-aware conversations\n";
        systemPrompt += "- Vector-based memory search\n";
        systemPrompt += "\nAvailable tools:\n";
        systemPrompt += "- navigate_to_url: Navigate to websites and extract content\n";
        systemPrompt += "- search_web: Search the web using Google\n";
        systemPrompt += "- extract_text_from_html: Extract clean text from HTML\n";
        systemPrompt += "\nUse these tools proactively to answer user questions that require real-time information!";

        return systemPrompt;
    }

    // Helper methods for direct Claude API calls (no agent system)
    
    private async Task<ConversationContext> CreateDirectClaudeConversationAsync(string sessionId)
    {
        // Check if direct Claude "agent" already exists in database
        var directClaudeAgentId = new Guid("00000000-0000-0000-0000-000000000001");
        var directClaudeAgent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == directClaudeAgentId);
        
        // Create the special direct Claude agent if it doesn't exist
        if (directClaudeAgent == null)
        {
            directClaudeAgent = new Agent
            {
                Id = directClaudeAgentId,
                Name = "Direct Claude API",
                Description = "Direct communication with Claude API without agent system",
                Instructions = "You are Claude, an AI assistant created by Anthropic. You are helpful, harmless, and honest.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.Agents.Add(directClaudeAgent);
            await _context.SaveChangesAsync();
        }

        // Create conversation with proper agent reference
        var conversation = new ConversationContext
        {
            SessionId = sessionId,
            AgentId = directClaudeAgentId,
            Agent = directClaudeAgent, // Properly set agent reference
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        _context.ConversationContexts.Add(conversation);
        await _context.SaveChangesAsync();
        return conversation;
    }
    
    private string BuildDirectClaudeSystemPrompt(List<Memory> context)
    {
        var systemPrompt = "You are Claude, an AI assistant created by Anthropic. You are helpful, harmless, and honest.";

        if (context.Any())
        {
            systemPrompt += "\n\nRelevant context from previous interactions:\n";
            foreach (var memory in context)
            {
                systemPrompt += $"- [{memory.Role}] {memory.Content}\n";
            }
        }

        systemPrompt += "\n\nCurrent capabilities:\n";
        systemPrompt += "- Goal tracking and management\n";
        systemPrompt += "- Workflow execution\n";
        systemPrompt += "- Context-aware conversations\n";
        systemPrompt += "- Vector-based memory search\n";
        systemPrompt += "\nAvailable tools:\n";
        systemPrompt += "- navigate_to_url: Navigate to websites and extract content\n";
        systemPrompt += "- search_web: Search the web using Google\n";
        systemPrompt += "- extract_text_from_html: Extract clean text from HTML\n";
        systemPrompt += "\nUse these tools proactively to answer user questions that require real-time information, weather data, current events, or any information that requires web access!";

        return systemPrompt;
    }
}

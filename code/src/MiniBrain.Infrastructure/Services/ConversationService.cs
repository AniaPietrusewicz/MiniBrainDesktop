using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly IMiniBrainDbContext _context;
    private readonly IClaudeApiService _claudeService;
    private readonly IVectorSearchService _vectorSearchService;

    public ConversationService(
        IMiniBrainDbContext context, 
        IClaudeApiService claudeService,
        IVectorSearchService vectorSearchService)
    {
        _context = context;
        _claudeService = claudeService;
        _vectorSearchService = vectorSearchService;
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
        await _context.SaveChangesAsync();

        return conversation;
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

        await _vectorSearchService.StoreVectorAsync(message.Id, content, new Dictionary<string, object>
        {
            ["type"] = "message",
            ["role"] = role,
            ["session_id"] = sessionId,
            ["timestamp"] = message.Timestamp
        });

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
        
        var relevantContext = await GetRelevantContextAsync(userMessage);
        
        var systemPrompt = BuildSystemPrompt(conversation.Agent, relevantContext);
        
        var response = await _claudeService.SendMessagesAsync(recentMessages, systemPrompt);

        await AddMessageAsync(sessionId, "assistant", response);

        return response;
    }

    private async Task<List<VectorSearchResult>> GetRelevantContextAsync(string query)
    {
        try
        {
            return await _vectorSearchService.SearchAsync(query, 5, 0.7);
        }
        catch
        {
            return new List<VectorSearchResult>();
        }
    }

    private string BuildSystemPrompt(Agent agent, List<VectorSearchResult> context)
    {
        var systemPrompt = agent.Instructions;

        if (context.Any())
        {
            systemPrompt += "\n\nRelevant context from previous interactions:\n";
            foreach (var item in context)
            {
                systemPrompt += $"- {item.Text}\n";
            }
        }

        systemPrompt += "\n\nCurrent capabilities:\n";
        systemPrompt += "- Goal tracking and management\n";
        systemPrompt += "- Workflow execution\n";
        systemPrompt += "- Context-aware conversations\n";
        systemPrompt += "- Vector-based memory search\n";

        return systemPrompt;
    }
}

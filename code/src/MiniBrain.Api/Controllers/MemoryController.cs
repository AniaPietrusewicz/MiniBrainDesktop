using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;

    public MemoryController(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchMemories([FromBody] SearchMemoriesRequest request)
    {
        try
        {
            var memories = await _memoryService.RetrieveMemoriesAsync(request.Query, request.Limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search/temporal")]
    public async Task<IActionResult> SearchMemoriesByTimeRange([FromBody] TemporalSearchRequest request)
    {
        try
        {
            var memories = await _memoryService.SearchMemoriesByTimeRangeAsync(
                request.StartTime, 
                request.EndTime, 
                request.Limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search/recent")]
    public async Task<IActionResult> SearchRecentMemories([FromBody] RecentSearchRequest request)
    {
        try
        {
            var memories = await _memoryService.SearchRecentMemoriesAsync(
                request.TimeSpan, 
                request.Limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search/hybrid")]
    public async Task<IActionResult> HybridSearch([FromBody] HybridSearchRequest request)
    {
        try
        {
            var memories = await _memoryService.HybridSearchAsync(
                request.Query,
                request.StartTime,
                request.EndTime,
                request.ConversationId,
                request.Threshold,
                request.Limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search/context-aware")]
    public async Task<IActionResult> ContextAwareSearch([FromBody] ContextAwareSearchRequest request)
    {
        try
        {
            var memories = await _memoryService.ContextAwareSearchAsync(
                request.Query,
                request.CurrentConversationId,
                request.SessionId,
                request.Limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("similar")]
    public async Task<IActionResult> SearchSimilarMemories([FromBody] SimilarSearchRequest request)
    {
        try
        {
            var memories = await _memoryService.SearchSimilarMemoriesAsync(
                request.Content, 
                request.Threshold);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{memoryId}")]
    public async Task<IActionResult> DeleteMemory(string memoryId)
    {
        try
        {
            var success = await _memoryService.DeleteMemoryAsync(memoryId);
            return success ? Ok(new { message = "Memory deleted successfully" }) : 
                           NotFound(new { message = "Memory not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("conversation/{conversationId}")]
    public async Task<IActionResult> GetConversationHistory(string conversationId)
    {
        try
        {
            var conversation = await _memoryService.GetConversationHistoryAsync(conversationId);
            return Ok(conversation);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search/intelligent")]
    public async Task<IActionResult> IntelligentSearch([FromBody] IntelligentSearchRequest request)
    {
        try
        {
            // This endpoint is designed to be used by the AI as a tool
            // The AI provides context about what type of search it wants to perform
            var memories = await ExecuteIntelligentSearch(request);
            return Ok(new { 
                memories = memories,
                strategy_used = request.PreferredStrategy,
                context = request.SearchContext
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<List<Memory>> ExecuteIntelligentSearch(IntelligentSearchRequest request)
    {
        // Execute the search based on the AI's preferred strategy
        return request.PreferredStrategy.ToLower() switch
        {
            "context-aware" => await _memoryService.ContextAwareSearchAsync(
                request.Query, 
                request.ConversationId ?? string.Empty, 
                request.SessionId ?? string.Empty, 
                request.Limit),
            
            "hybrid" => await _memoryService.HybridSearchAsync(
                request.Query,
                request.StartTime,
                request.EndTime,
                request.ConversationId,
                request.Threshold,
                request.Limit),
            
            "temporal" => await _memoryService.SearchMemoriesByTimeRangeAsync(
                request.StartTime ?? DateTime.UtcNow.AddDays(-30),
                request.EndTime ?? DateTime.UtcNow,
                request.Limit),
            
            "recent" => await _memoryService.SearchRecentMemoriesAsync(
                request.TimeSpan ?? TimeSpan.FromHours(24),
                request.Limit),
            
            "similar" => await _memoryService.SearchSimilarMemoriesAsync(
                request.Query,
                request.Threshold),
            
            _ => await _memoryService.RetrieveMemoriesAsync(request.Query, request.Limit)
        };
    }
}

public class SearchMemoriesRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 10;
}

public class TemporalSearchRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Limit { get; set; } = 10;
}

public class RecentSearchRequest
{
    public TimeSpan TimeSpan { get; set; }
    public int Limit { get; set; } = 10;
}

public class HybridSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ConversationId { get; set; }
    public float Threshold { get; set; } = 0.8f;
    public int Limit { get; set; } = 10;
}

public class ContextAwareSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string CurrentConversationId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int Limit { get; set; } = 10;
}

public class SimilarSearchRequest
{
    public string Content { get; set; } = string.Empty;
    public float Threshold { get; set; } = 0.8f;
}

public class IntelligentSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string PreferredStrategy { get; set; } = "semantic"; // semantic, context-aware, hybrid, temporal, recent, similar
    public string SearchContext { get; set; } = string.Empty; // AI's reasoning for strategy choice
    public string? ConversationId { get; set; }
    public string? SessionId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? TimeSpan { get; set; }
    public float Threshold { get; set; } = 0.8f;
    public int Limit { get; set; } = 10;
}

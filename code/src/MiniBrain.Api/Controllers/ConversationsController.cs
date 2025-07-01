using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.DTOs;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var response = await _conversationService.ProcessMessageAsync(request.SessionId, request.Message);
            return Ok(new { response, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{sessionId}/history")]
    public async Task<IActionResult> GetConversationHistory(string sessionId, [FromQuery] int limit = 50)
    {
        try
        {
            var history = await _conversationService.GetConversationHistoryAsync(sessionId, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetConversation(string sessionId)
    {
        var conversation = await _conversationService.GetConversationAsync(sessionId);
        return conversation == null ? NotFound() : Ok(conversation);
    }
}

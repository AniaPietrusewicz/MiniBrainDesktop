using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.DTOs;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentsController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        try
        {
            var agent = await _agentService.CreateAgentAsync(request.Name, request.Description ?? string.Empty, request.Instructions);
            return CreatedAtAction(nameof(GetAgent), new { id = agent.Id }, agent);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAgent(Guid id)
    {
        var agent = await _agentService.GetAgentAsync(id);
        return agent == null ? NotFound() : Ok(agent);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAgents()
    {
        var agents = await _agentService.GetAllAgentsAsync();
        return Ok(agents);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAgent(Guid id)
    {
        var result = await _agentService.DeleteAgentAsync(id);
        return result ? NoContent() : NotFound();
    }
}

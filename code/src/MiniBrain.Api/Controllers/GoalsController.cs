using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.DTOs;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoalsController : ControllerBase
{
    private readonly IGoalService _goalService;

    public GoalsController(IGoalService goalService)
    {
        _goalService = goalService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
    {
        try
        {
            var goal = await _goalService.CreateGoalAsync(request.AgentId, request.Title, request.Description ?? string.Empty, request.Priority);
            return CreatedAtAction(nameof(GetGoal), new { id = goal.Id }, goal);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGoal(Guid id)
    {
        var goal = await _goalService.GetGoalAsync(id);
        return goal == null ? NotFound() : Ok(goal);
    }

    [HttpGet("agent/{agentId}")]
    public async Task<IActionResult> GetGoalsByAgent(Guid agentId)
    {
        var goals = await _goalService.GetGoalsByAgentAsync(agentId);
        return Ok(goals);
    }

    [HttpPost("{goalId}/steps")]
    public async Task<IActionResult> AddGoalStep(Guid goalId, [FromBody] AddGoalStepRequest request)
    {
        try
        {
            var goal = await _goalService.AddGoalStepAsync(goalId, request.Description, request.OrderIndex);
            return Ok(goal);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("steps/{stepId}/complete")]
    public async Task<IActionResult> CompleteGoalStep(Guid stepId)
    {
        var result = await _goalService.CompleteGoalStepAsync(stepId);
        return result ? Ok() : NotFound();
    }
}

public class AddGoalStepRequest
{
    public required string Description { get; set; }
    public int OrderIndex { get; set; }
}

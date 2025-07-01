using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.DTOs;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        try
        {
            var workflow = await _workflowService.CreateWorkflowAsync(request.AgentId, request.Name, request.Description ?? string.Empty);
            return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkflow(Guid id)
    {
        var workflow = await _workflowService.GetWorkflowAsync(id);
        return workflow == null ? NotFound() : Ok(workflow);
    }

    [HttpGet("agent/{agentId}")]
    public async Task<IActionResult> GetWorkflowsByAgent(Guid agentId)
    {
        var workflows = await _workflowService.GetWorkflowsByAgentAsync(agentId);
        return Ok(workflows);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteWorkflow([FromBody] ExecuteWorkflowRequest request)
    {
        try
        {
            var execution = await _workflowService.ExecuteWorkflowAsync(request.WorkflowId, request.InputData);
            return Ok(execution);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("execution/{executionId}")]
    public async Task<IActionResult> GetWorkflowExecution(Guid executionId)
    {
        var execution = await _workflowService.GetWorkflowExecutionAsync(executionId);
        return execution == null ? NotFound() : Ok(execution);
    }
}

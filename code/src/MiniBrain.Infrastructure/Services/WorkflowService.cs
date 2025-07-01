using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;
using System.Text.Json;

namespace MiniBrain.Infrastructure.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IMiniBrainDbContext _context;
    private readonly IClaudeApiService _claudeService;

    public WorkflowService(IMiniBrainDbContext context, IClaudeApiService claudeService)
    {
        _context = context;
        _claudeService = claudeService;
    }

    public async Task<Workflow> CreateWorkflowAsync(Guid agentId, string name, string description)
    {
        var workflow = new Workflow
        {
            AgentId = agentId,
            Name = name,
            Description = description
        };

        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();

        return workflow;
    }

    public async Task<Workflow?> GetWorkflowAsync(Guid id)
    {
        return await _context.Workflows
            .Include(w => w.Agent)
            .Include(w => w.Steps)
            .Include(w => w.Executions)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<List<Workflow>> GetWorkflowsByAgentAsync(Guid agentId)
    {
        return await _context.Workflows
            .Include(w => w.Steps)
            .Where(w => w.AgentId == agentId && w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<WorkflowExecution> ExecuteWorkflowAsync(Guid workflowId, string inputData)
    {
        var workflow = await GetWorkflowAsync(workflowId);
        if (workflow == null)
            throw new ArgumentException($"Workflow {workflowId} not found");

        var execution = new WorkflowExecution
        {
            WorkflowId = workflowId,
            Status = ExecutionStatus.Running,
            InputData = inputData
        };

        _context.WorkflowExecutions.Add(execution);
        await _context.SaveChangesAsync();

        try
        {
            var orderedSteps = workflow.Steps.OrderBy(s => s.OrderIndex).ToList();
            var currentData = inputData;

            foreach (var step in orderedSteps)
            {
                var stepExecution = await ExecuteStepAsync(step, execution.Id, currentData);
                
                if (stepExecution.Status == ExecutionStatus.Failed && step.IsRequired)
                {
                    execution.Status = ExecutionStatus.Failed;
                    execution.ErrorMessage = stepExecution.ErrorMessage;
                    break;
                }

                if (stepExecution.Status == ExecutionStatus.Completed)
                {
                    currentData = stepExecution.OutputData;
                }
            }

            if (execution.Status == ExecutionStatus.Running)
            {
                execution.Status = ExecutionStatus.Completed;
                execution.OutputData = currentData;
            }

            execution.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return execution;
        }
        catch (Exception ex)
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            throw;
        }
    }

    public async Task<WorkflowExecution?> GetWorkflowExecutionAsync(Guid executionId)
    {
        return await _context.WorkflowExecutions
            .Include(e => e.Workflow)
            .Include(e => e.StepExecutions)
                .ThenInclude(se => se.WorkflowStep)
            .FirstOrDefaultAsync(e => e.Id == executionId);
    }

    private async Task<StepExecution> ExecuteStepAsync(WorkflowStep step, Guid executionId, string inputData)
    {
        var stepExecution = new StepExecution
        {
            WorkflowStepId = step.Id,
            WorkflowExecutionId = executionId,
            Status = ExecutionStatus.Running,
            InputData = inputData
        };

        _context.StepExecutions.Add(stepExecution);
        await _context.SaveChangesAsync();

        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= step.MaxRetries)
        {
            try
            {
                stepExecution.AttemptNumber = retryCount + 1;
                
                var result = await ExecuteStepActionAsync(step, inputData);
                
                stepExecution.Status = ExecutionStatus.Completed;
                stepExecution.OutputData = result;
                stepExecution.CompletedAt = DateTime.UtcNow;
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                
                if (retryCount <= step.MaxRetries)
                {
                    stepExecution.Status = ExecutionStatus.Retrying;
                    await Task.Delay(1000 * retryCount);
                }
                else
                {
                    stepExecution.Status = ExecutionStatus.Failed;
                    stepExecution.ErrorMessage = ex.Message;
                    stepExecution.CompletedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();
        return stepExecution;
    }

    private async Task<string> ExecuteStepActionAsync(WorkflowStep step, string inputData)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(step.Parameters) ?? new();
        
        return step.ActionType.ToLowerInvariant() switch
        {
            "claude_message" => await ExecuteClaudeMessageAction(parameters, inputData),
            "data_transform" => ExecuteDataTransformAction(parameters, inputData),
            "delay" => await ExecuteDelayAction(parameters),
            "conditional" => ExecuteConditionalAction(parameters, inputData),
            _ => throw new NotSupportedException($"Action type '{step.ActionType}' is not supported")
        };
    }

    private async Task<string> ExecuteClaudeMessageAction(Dictionary<string, object> parameters, string inputData)
    {
        var prompt = parameters.GetValueOrDefault("prompt", "").ToString();
        var systemPrompt = parameters.GetValueOrDefault("system_prompt", "").ToString();
        
        var message = prompt.Replace("{input}", inputData);
        var response = await _claudeService.SendMessageAsync(message, 
            string.IsNullOrEmpty(systemPrompt) ? null : systemPrompt);
        
        return JsonSerializer.Serialize(new { response, timestamp = DateTime.UtcNow });
    }

    private string ExecuteDataTransformAction(Dictionary<string, object> parameters, string inputData)
    {
        var transform = parameters.GetValueOrDefault("transform", "").ToString();
        
        return transform.ToLowerInvariant() switch
        {
            "to_upper" => inputData.ToUpperInvariant(),
            "to_lower" => inputData.ToLowerInvariant(),
            "trim" => inputData.Trim(),
            "json_parse" => JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(inputData)),
            _ => inputData
        };
    }

    private async Task<string> ExecuteDelayAction(Dictionary<string, object> parameters)
    {
        var delayMs = Convert.ToInt32(parameters.GetValueOrDefault("delay_ms", 1000));
        await Task.Delay(delayMs);
        return JsonSerializer.Serialize(new { delayed = true, delay_ms = delayMs });
    }

    private string ExecuteConditionalAction(Dictionary<string, object> parameters, string inputData)
    {
        var condition = parameters.GetValueOrDefault("condition", "").ToString();
        var trueValue = parameters.GetValueOrDefault("true_value", inputData).ToString();
        var falseValue = parameters.GetValueOrDefault("false_value", inputData).ToString();
        
        var result = condition.ToLowerInvariant() switch
        {
            "not_empty" => !string.IsNullOrWhiteSpace(inputData),
            "is_json" => IsValidJson(inputData),
            _ => false
        };
        
        return result ? trueValue! : falseValue!;
    }

    private static bool IsValidJson(string input)
    {
        try
        {
            JsonSerializer.Deserialize<object>(input);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

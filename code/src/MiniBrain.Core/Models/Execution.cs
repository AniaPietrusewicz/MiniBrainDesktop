using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Retrying
}

public class WorkflowExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid WorkflowId { get; set; }
    
    public Workflow Workflow { get; set; } = null!;
    
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string InputData { get; set; } = "{}";
    
    public string OutputData { get; set; } = "{}";
    
    public List<StepExecution> StepExecutions { get; set; } = new();
}

public class StepExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid WorkflowStepId { get; set; }
    
    public WorkflowStep WorkflowStep { get; set; } = null!;
    
    public Guid WorkflowExecutionId { get; set; }
    
    public WorkflowExecution WorkflowExecution { get; set; } = null!;
    
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string InputData { get; set; } = "{}";
    
    public string OutputData { get; set; } = "{}";
    
    public int AttemptNumber { get; set; } = 1;
}

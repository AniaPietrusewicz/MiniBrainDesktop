using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public class WorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    
    [Required]
    public required string ActionType { get; set; }
    
    public string Parameters { get; set; } = "{}";
    
    public int OrderIndex { get; set; }
    
    public bool IsRequired { get; set; } = true;
    
    public int MaxRetries { get; set; } = 3;
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public Guid WorkflowId { get; set; }
    
    public Workflow Workflow { get; set; } = null!;
    
    public List<StepExecution> Executions { get; set; } = new();
}

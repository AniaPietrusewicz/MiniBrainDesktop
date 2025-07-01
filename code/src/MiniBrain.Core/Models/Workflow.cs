using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public Guid AgentId { get; set; }
    
    public Agent Agent { get; set; } = null!;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<WorkflowStep> Steps { get; set; } = new();
    
    public List<WorkflowExecution> Executions { get; set; } = new();
}

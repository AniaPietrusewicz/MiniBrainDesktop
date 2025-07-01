using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.DTOs;

public class CreateAgentRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public required string Instructions { get; set; }
}

public class CreateWorkflowRequest
{
    [Required]
    public Guid AgentId { get; set; }
    
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
}

public class CreateGoalRequest
{
    [Required]
    public Guid AgentId { get; set; }
    
    [Required, MaxLength(200)]
    public required string Title { get; set; }
    
    public string? Description { get; set; }
    
    public int Priority { get; set; } = 5;
    
    public DateTime? DueDate { get; set; }
}

public class SendMessageRequest
{
    [Required]
    public required string SessionId { get; set; }
    
    [Required]
    public required string Message { get; set; }
    
    public Guid? AgentId { get; set; }
}

public class ExecuteWorkflowRequest
{
    [Required]
    public Guid WorkflowId { get; set; }
    
    public string InputData { get; set; } = "{}";
}

public class VectorSearchRequest
{
    [Required]
    public required string Query { get; set; }
    
    public int Limit { get; set; } = 10;
    
    public double Threshold { get; set; } = 0.7;
}

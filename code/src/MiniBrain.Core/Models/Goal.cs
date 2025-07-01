using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public enum GoalStatus
{
    Active,
    Completed,
    Failed,
    Cancelled,
    OnHold
}

public class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(200)]
    public required string Title { get; set; }
    
    public string? Description { get; set; }
    
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    
    public int Priority { get; set; } = 5;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? DueDate { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public Guid AgentId { get; set; }
    
    public Agent Agent { get; set; } = null!;
    
    public List<GoalStep> Steps { get; set; } = new();
}

public class GoalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(200)]
    public required string Description { get; set; }
    
    public bool IsCompleted { get; set; } = false;
    
    public int OrderIndex { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public Guid GoalId { get; set; }
    
    public Goal Goal { get; set; } = null!;
}

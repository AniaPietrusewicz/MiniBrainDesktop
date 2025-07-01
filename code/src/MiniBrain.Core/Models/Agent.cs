using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(100)]
    public required string Name { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public string Instructions { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<Workflow> Workflows { get; set; } = new();
    
    public List<Goal> Goals { get; set; } = new();
    
    public List<ConversationContext> ConversationContexts { get; set; } = new();
}

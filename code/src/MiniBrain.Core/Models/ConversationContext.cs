using System.ComponentModel.DataAnnotations;

namespace MiniBrain.Core.Models;

public class ConversationContext
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(100)]
    public required string SessionId { get; set; }
    
    public Guid AgentId { get; set; }
    
    public Agent Agent { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    public string ContextData { get; set; } = "{}";
    
    public List<Message> Messages { get; set; } = new();
    
    // Memory architecture integration - following the architecture spec
    public DateTime? EndTime { get; set; }
    
    public string? Summary { get; set; }
    
    public List<string> Tags { get; set; } = new();
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public int MessageCount => Messages.Count;
    
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - CreatedAt;
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public required string Role { get; set; }
    
    [Required]
    public required string Content { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public Guid ConversationContextId { get; set; }
    
    public ConversationContext ConversationContext { get; set; } = null!;
}

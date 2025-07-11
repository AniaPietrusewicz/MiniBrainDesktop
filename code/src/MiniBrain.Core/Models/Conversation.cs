namespace MiniBrain.Core.Models;

public class Conversation
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Memory> Messages { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

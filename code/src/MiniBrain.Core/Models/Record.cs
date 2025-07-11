namespace MiniBrain.Core.Models;

public class Record
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
    public float[] Vector { get; set; } = Array.Empty<float>();
}

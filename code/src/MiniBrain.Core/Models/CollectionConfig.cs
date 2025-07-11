namespace MiniBrain.Core.Models;

public class CollectionConfig
{
    public string Name { get; set; } = string.Empty;
    public int VectorSize { get; set; }
    public string Distance { get; set; } = "Cosine";
    public Dictionary<string, object> Metadata { get; set; } = new();
}

namespace MiniBrain.Core.Interfaces;

public interface IQdrantHealthService
{
    Task<bool> IsQdrantHealthyAsync();
    Task<bool> TryStartQdrantAsync();
    Task<bool> EnsureQdrantAvailableAsync();
    Task<QdrantHealthStatus> CheckHealthWithDetailsAsync();
}

public class QdrantHealthStatus
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool ContainerExists { get; set; }
    public bool ContainerRunning { get; set; }
    public bool HttpPortAccessible { get; set; }
    public bool GrpcPortAccessible { get; set; }
}

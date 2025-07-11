using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class QdrantHealthService : IQdrantHealthService
{
    private readonly QdrantSettings _qdrantSettings;
    private readonly ILogger<QdrantHealthService> _logger;
    private readonly HttpClient _httpClient;

    public QdrantHealthService(
        IOptions<QdrantSettings> qdrantSettings,
        ILogger<QdrantHealthService> logger,
        HttpClient httpClient)
    {
        _qdrantSettings = qdrantSettings.Value;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<bool> IsQdrantHealthyAsync()
    {
        if (!_qdrantSettings.Enabled)
        {
            _logger.LogInformation("🚫 Qdrant is disabled in configuration");
            return false;
        }

        try
        {
            _logger.LogInformation("Checking Qdrant health at {BaseUrl}", _qdrantSettings.BaseUrl);
            
            // Qdrant root endpoint returns basic info when healthy
            var response = await _httpClient.GetAsync($"{_qdrantSettings.BaseUrl}/");
            var isHealthy = response.IsSuccessStatusCode;
            
            if (isHealthy)
            {
                _logger.LogInformation("✅ Qdrant is healthy and responding");
            }
            else
            {
                _logger.LogWarning("⚠️ Qdrant health check failed with status: {StatusCode}", response.StatusCode);
            }
            
            return isHealthy;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("❌ Qdrant connection failed: {Message}", ex.Message);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("⏰ Qdrant health check timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Unexpected error during Qdrant health check");
            return false;
        }
    }

    public async Task<bool> TryStartQdrantAsync()
    {
        if (!_qdrantSettings.Enabled)
        {
            _logger.LogInformation("🚫 Qdrant is disabled - skipping start attempt");
            return false;
        }

        try
        {
            _logger.LogInformation("🚀 Attempting to start Qdrant Docker container...");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "start qdrant",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("❌ Failed to start docker process");
                return false;
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("✅ Successfully started Qdrant container: {Output}", output.Trim());
                
                await Task.Delay(3000);
                
                for (int i = 0; i < 10; i++)
                {
                    if (await IsQdrantHealthyAsync())
                    {
                        _logger.LogInformation("🎉 Qdrant is now healthy after startup");
                        return true;
                    }
                    _logger.LogInformation("⏳ Waiting for Qdrant to become healthy... ({Attempt}/10)", i + 1);
                    await Task.Delay(2000);
                }
                
                _logger.LogWarning("⚠️ Qdrant container started but health check still failing");
                return false;
            }
            else
            {
                _logger.LogError("❌ Failed to start Qdrant container. Exit code: {ExitCode}, Error: {Error}", 
                    process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error starting Qdrant container");
            return false;
        }
    }

    public async Task<bool> EnsureQdrantAvailableAsync()
    {
        if (!_qdrantSettings.Enabled)
        {
            _logger.LogInformation("🚫 Qdrant is disabled in configuration - skipping availability check");
            return false;
        }

        _logger.LogInformation("🔍 Ensuring Qdrant is available...");
        
        if (await IsQdrantHealthyAsync())
        {
            return true;
        }

        _logger.LogWarning("⚠️ Qdrant is not healthy, attempting automatic recovery...");
        
        if (await TryStartQdrantAsync())
        {
            _logger.LogInformation("✅ Qdrant auto-recovery successful");
            return true;
        }
        
        _logger.LogError("❌ Qdrant auto-recovery failed. Manual intervention required.");
        _logger.LogError("💡 Try running: docker start qdrant");
        return false;
    }

    public async Task<QdrantHealthStatus> CheckHealthWithDetailsAsync()
    {
        var status = new QdrantHealthStatus();
        
        if (!_qdrantSettings.Enabled)
        {
            status.Status = "Disabled";
            status.ErrorMessage = "Qdrant is disabled in configuration (Qdrant:Enabled = false)";
            status.IsHealthy = false;
            return status;
        }
        
        try
        {
            // Check container existence and status
            var containerCheck = await CheckContainerStatusAsync();
            status.ContainerExists = containerCheck.exists;
            status.ContainerRunning = containerCheck.running;
            
            if (!status.ContainerExists)
            {
                status.Status = "Container not found";
                status.ErrorMessage = "Qdrant Docker container does not exist";
                return status;
            }
            
            if (!status.ContainerRunning)
            {
                status.Status = "Container stopped";
                status.ErrorMessage = "Qdrant Docker container exists but is not running";
                return status;
            }
            
            // Check HTTP port accessibility
            status.HttpPortAccessible = await CheckPortAccessibilityAsync(_qdrantSettings.BaseUrl);
            
            // Check gRPC port accessibility (assuming it's HTTP port + 1)
            var grpcUrl = _qdrantSettings.BaseUrl.Replace("6333", "6334");
            status.GrpcPortAccessible = await CheckPortAccessibilityAsync(grpcUrl);
            
            status.IsHealthy = status.ContainerRunning && status.HttpPortAccessible && status.GrpcPortAccessible;
            status.Status = status.IsHealthy ? "Healthy" : "Unhealthy";
            
            if (!status.HttpPortAccessible)
                status.ErrorMessage += "HTTP port (6333) not accessible. ";
            if (!status.GrpcPortAccessible)
                status.ErrorMessage += "gRPC port (6334) not accessible. ";
                
        }
        catch (Exception ex)
        {
            status.ErrorMessage = ex.Message;
            status.Status = "Error during health check";
            _logger.LogError(ex, "Error checking Qdrant health details");
        }
        
        return status;
    }

    private async Task<(bool exists, bool running)> CheckContainerStatusAsync()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps -a --filter name=qdrant --format \"{{.Status}}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            if (process == null) return (false, false);
            
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            
            var exists = !string.IsNullOrWhiteSpace(output.Trim());
            var running = output.Contains("Up");
            
            return (exists, running);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking container status");
            return (false, false);
        }
    }

    private async Task<bool> CheckPortAccessibilityAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{url}/", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

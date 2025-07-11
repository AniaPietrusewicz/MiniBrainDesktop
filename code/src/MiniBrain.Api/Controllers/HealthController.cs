using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IQdrantHealthService _qdrantHealthService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IQdrantHealthService qdrantHealthService,
        ILogger<HealthController> logger)
    {
        _qdrantHealthService = qdrantHealthService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealthStatus()
    {
        var qdrantHealthy = await _qdrantHealthService.IsQdrantHealthyAsync();
        
        var healthStatus = new
        {
            status = qdrantHealthy ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            services = new
            {
                api = "healthy",
                qdrant = qdrantHealthy ? "healthy" : "unhealthy"
            }
        };

        return qdrantHealthy ? Ok(healthStatus) : StatusCode(503, healthStatus);
    }

    [HttpGet("qdrant")]
    public async Task<IActionResult> GetQdrantHealth()
    {
        var isHealthy = await _qdrantHealthService.IsQdrantHealthyAsync();
        
        var result = new
        {
            service = "qdrant",
            status = isHealthy ? "healthy" : "unhealthy",
            timestamp = DateTime.UtcNow
        };

        return isHealthy ? Ok(result) : StatusCode(503, result);
    }

    [HttpGet("qdrant/detailed")]
    public async Task<IActionResult> GetQdrantDetailedHealth()
    {
        try
        {
            var healthStatus = await _qdrantHealthService.CheckHealthWithDetailsAsync();
            
            if (healthStatus.IsHealthy)
            {
                return Ok(healthStatus);
            }
            else
            {
                return StatusCode(503, healthStatus); // Service Unavailable
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking detailed Qdrant health");
            return StatusCode(500, new { error = "Failed to check Qdrant health", message = ex.Message });
        }
    }

    [HttpGet("qdrant/ensure")]
    public async Task<IActionResult> EnsureQdrantAvailable()
    {
        try
        {
            var success = await _qdrantHealthService.EnsureQdrantAvailableAsync();
            
            if (success)
            {
                return Ok(new { message = "Qdrant is available", timestamp = DateTime.UtcNow });
            }
            else
            {
                return StatusCode(503, new { error = "Qdrant is not available", timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Qdrant availability");
            return StatusCode(500, new { error = "Failed to ensure Qdrant availability", message = ex.Message });
        }
    }

    [HttpPost("qdrant/restart")]
    public async Task<IActionResult> RestartQdrant()
    {
        _logger.LogInformation("Manual Qdrant restart requested");
        
        var success = await _qdrantHealthService.TryStartQdrantAsync();
        
        if (success)
        {
            return Ok(new { message = "Qdrant restarted successfully", timestamp = DateTime.UtcNow });
        }
        else
        {
            return StatusCode(500, new { message = "Failed to restart Qdrant", timestamp = DateTime.UtcNow });
        }
    }
}

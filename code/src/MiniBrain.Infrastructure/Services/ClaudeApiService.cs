using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ClaudeApiService> _logger;

    public ClaudeApiService(HttpClient httpClient, IOptions<ClaudeApiSettings> settings, ILogger<ClaudeApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        
        _logger.LogInformation("Initializing Claude API Service with base URL: {BaseUrl}, Model: {Model}", 
        _settings.BaseUrl, _settings.Model);
        _logger.LogDebug("API Key starts with: {ApiKeyPrefix}", 
            string.IsNullOrEmpty(_settings.ApiKey) ? "NULL" : _settings.ApiKey.Substring(0, Math.Min(20, _settings.ApiKey.Length)) + "...");
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", _settings.Version);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    public async Task<string> SendMessageAsync(string message, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var messages = new List<ClaudeMessage>
        {
            new() { Role = "user", Content = message }
        };

        return await SendMessagesInternalAsync(messages, systemPrompt, cancellationToken);
    }

    public async Task<string> SendMessagesAsync(IEnumerable<Message> messages, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var claudeMessages = messages.Select(m => new ClaudeMessage
        {
            Role = m.Role.ToLowerInvariant(),
            Content = m.Content
        }).ToList();

        return await SendMessagesInternalAsync(claudeMessages, systemPrompt, cancellationToken);
    }

    private async Task<string> SendMessagesInternalAsync(List<ClaudeMessage> messages, string? systemPrompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Claude API request with {MessageCount} messages", messages.Count);
        
        var request = new ClaudeRequest
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Temperature = _settings.Temperature,
            Messages = messages
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.System = systemPrompt;
            _logger.LogDebug("Using system prompt: {SystemPrompt}", systemPrompt);
        }

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        _logger.LogDebug("Claude API request JSON: {RequestJson}", json);
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            _logger.LogInformation("Sending POST request to Claude API at {Url}", "/v1/messages");
            _logger.LogDebug("Request headers: x-api-key={ApiKeyPrefix}..., anthropic-version={Version}", 
                string.IsNullOrEmpty(_settings.ApiKey) ? "NULL" : _settings.ApiKey.Substring(0, Math.Min(20, _settings.ApiKey.Length)), _settings.Version);
            var startTime = DateTime.UtcNow;
            
            var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Claude API response received in {Duration}ms with status {StatusCode}", 
                duration.TotalMilliseconds, response.StatusCode);

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Claude API raw response: {ResponseJson}", responseJson);

            response.EnsureSuccessStatusCode();

            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, _jsonOptions);
            var responseText = claudeResponse?.Content?.FirstOrDefault()?.Text ?? string.Empty;
            
            _logger.LogInformation("Claude API response parsed successfully, text length: {Length}", responseText.Length);
            _logger.LogDebug("Claude response text: {ResponseText}", responseText);

            return responseText;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("authentication_error") || ex.Message.Contains("invalid x-api-key"))
        {
            _logger.LogError(ex, "Claude API authentication error");
            var errorMessage = $"Authentication error occurred: {ex.Message}";
            _logger.LogInformation("Returning authentication error message: {ErrorMessage}", errorMessage);
            return errorMessage;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Claude API HTTP request failed");
            throw new InvalidOperationException($"Claude API request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Claude API request timed out");
            throw new TimeoutException($"Claude API request timed out: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude API response");
            throw new InvalidOperationException($"Failed to parse Claude API response: {ex.Message}", ex);
        }
    }
}

internal class ClaudeRequest
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
    public string? System { get; set; }
    public List<ClaudeMessage> Messages { get; set; } = new();
}

internal class ClaudeMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class ClaudeResponse
{
    public List<ClaudeContent>? Content { get; set; }
}

internal class ClaudeContent
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

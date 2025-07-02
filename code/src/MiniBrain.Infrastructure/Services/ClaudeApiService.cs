using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public ClaudeApiService(HttpClient httpClient, IOptions<ClaudeApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        
        var apiKey = _settings.ApiKeyEncoded 
            ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_settings.ApiKey))
            : _settings.ApiKey;
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
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
        }

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/v1/messages", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, _jsonOptions);

            return claudeResponse?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Claude API request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException($"Claude API request timed out: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
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

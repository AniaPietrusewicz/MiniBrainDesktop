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
    private readonly ClaudeToolExecutor _toolExecutor;

    public ClaudeApiService(HttpClient httpClient, IOptions<ClaudeApiSettings> settings, ILogger<ClaudeApiService> logger, IWebBrowsingService webBrowsingService)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _toolExecutor = new ClaudeToolExecutor(webBrowsingService);
        
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

    public async Task<string> SendMessageWithToolsAsync(string message, string? systemPrompt = null, bool enableTools = true, CancellationToken cancellationToken = default)
    {
        var messages = new List<ClaudeMessage>
        {
            new() { Role = "user", Content = message }
        };

        return await SendMessagesWithToolsInternalAsync(messages, systemPrompt, enableTools, cancellationToken);
    }

    public async Task<string> SendMessagesWithToolsAsync(IEnumerable<Message> messages, string? systemPrompt = null, bool enableTools = true, CancellationToken cancellationToken = default)
    {
        var claudeMessages = messages.Select(m => new ClaudeMessage
        {
            Role = m.Role.ToLowerInvariant(),
            Content = m.Content
        }).ToList();

        return await SendMessagesWithToolsInternalAsync(claudeMessages, systemPrompt, enableTools, cancellationToken);
    }

    private async Task<string> SendMessagesWithToolsInternalAsync(List<ClaudeMessage> messages, string? systemPrompt, bool enableTools, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Claude API request with tools enabled: {EnableTools}, Messages: {MessageCount}", enableTools, messages.Count);
        
        var request = new ClaudeRequestWithTools
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Temperature = _settings.Temperature,
            Messages = messages,
            Tools = enableTools ? ClaudeToolExecutor.GetAvailableTools() : null
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.System = systemPrompt;
            _logger.LogDebug("Using system prompt: {SystemPrompt}", systemPrompt);
        }

        var conversationMessages = new List<ClaudeMessage>(messages);
        const int maxToolCalls = 5;
        var toolCallCount = 0;

        while (toolCallCount < maxToolCalls)
        {
            request.Messages = conversationMessages;
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
                
                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponseWithTools>(responseJson, _jsonOptions);
                
                if (claudeResponse?.Content == null || !claudeResponse.Content.Any())
                {
                    _logger.LogWarning("Claude API returned empty content");
                    return "No response content received from Claude API";
                }

                var hasToolUse = false;
                var responseText = new StringBuilder();
                
                foreach (var contentItem in claudeResponse.Content)
                {
                    if (contentItem.Type == "text")
                    {
                        responseText.AppendLine(contentItem.Text);
                    }
                    else if (contentItem.Type == "tool_use" && enableTools)
                    {
                        hasToolUse = true;
                        toolCallCount++;
                        
                        conversationMessages.Add(new ClaudeMessage
                        {
                            Role = "assistant",
                            Content = JsonSerializer.Serialize(contentItem)
                        });

                        _logger.LogInformation("Executing tool: {ToolName} with ID: {ToolId}", contentItem.Name, contentItem.Id);
                        
                        var toolResult = await _toolExecutor.ExecuteToolAsync(contentItem.Name, contentItem.Input, cancellationToken);
                        
                        conversationMessages.Add(new ClaudeMessage
                        {
                            Role = "user",
                            Content = JsonSerializer.Serialize(new ClaudeToolResult
                            {
                                Type = "tool_result",
                                ToolUseId = contentItem.Id,
                                Content = toolResult
                            })
                        });

                        _logger.LogDebug("Tool {ToolName} executed, result length: {Length}", contentItem.Name, toolResult.Length);
                    }
                }

                if (!hasToolUse)
                {
                    var finalResponse = responseText.ToString().Trim();
                    _logger.LogInformation("Claude API response completed, text length: {Length}", finalResponse.Length);
                    return finalResponse;
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("authentication_error") || ex.Message.Contains("invalid x-api-key"))
            {
                _logger.LogError(ex, "Claude API authentication error");
                return $"Authentication error occurred: {ex.Message}";
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

        _logger.LogWarning("Maximum tool calls ({MaxCalls}) reached, ending conversation", maxToolCalls);
        return "Tool execution limit reached. The conversation has been terminated to prevent infinite loops.";
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

internal class ClaudeRequestWithTools : ClaudeRequest
{
    public List<ClaudeTool>? Tools { get; set; }
}

internal class ClaudeMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class ClaudeResponse
{
    public List<ClaudeContentItem>? Content { get; set; }
}

internal class ClaudeResponseWithTools : ClaudeResponse
{
    public string StopReason { get; set; } = string.Empty;
}

internal class ClaudeContentItem
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
}

internal class ClaudeContent
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}



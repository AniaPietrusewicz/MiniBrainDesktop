using System.Text.Json;
using System.Text;
using System.Diagnostics;
using MiniBrain.Orchestration.Interfaces;
using MiniBrain.Orchestration.Models;

namespace MiniBrain.Orchestration.Services;

public class WebBrowserService : IWebBrowserService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebBrowserService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Set a realistic user agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Parse the input to determine the action
            var action = GetStringProperty(input, "action") ?? "navigate";
            var url = GetStringProperty(input, "url");
            
            if (string.IsNullOrEmpty(url))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = "URL is required for web browsing",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            // Ensure URL has a protocol
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            // Validate URL format
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"Invalid URL format: {url}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            switch (action.ToLowerInvariant())
            {
                case "navigate":
                case "get":
                    return await NavigateToUrl(uri, stopwatch, cancellationToken);
                
                case "headers":
                    return await GetHeaders(uri, stopwatch, cancellationToken);
                
                case "status":
                    return await CheckStatus(uri, stopwatch, cancellationToken);
                
                default:
                    return new ToolResult
                    {
                        Success = false,
                        Output = $"Unknown action: {action}. Supported actions: navigate, get, headers, status",
                        ExecutionTime = stopwatch.Elapsed
                    };
            }
        }
        catch (OperationCanceledException)
        {
            return new ToolResult
            {
                Success = false,
                Output = "Operation was cancelled",
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Error: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> NavigateToUrl(Uri uri, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            var result = new
            {
                Status = $"{(int)response.StatusCode} {response.StatusCode}",
                Url = uri.ToString(),
                ContentLength = content.Length,
                ContentType = response.Content.Headers.ContentType?.ToString(),
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                Content = TruncateContent(content, 5000) // Limit content size
            };

            return new ToolResult
            {
                Success = response.IsSuccessStatusCode,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"HTTP request failed: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new ToolResult
            {
                Success = false,
                Output = "Request timed out",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> GetHeaders(Uri uri, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            var result = new
            {
                Status = $"{(int)response.StatusCode} {response.StatusCode}",
                Url = uri.ToString(),
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                ContentHeaders = response.Content.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };

            return new ToolResult
            {
                Success = response.IsSuccessStatusCode,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"HTTP request failed: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> CheckStatus(Uri uri, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            var result = new
            {
                Status = $"{(int)response.StatusCode} {response.StatusCode}",
                Url = uri.ToString(),
                IsSuccessful = response.IsSuccessStatusCode,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true, // Always successful for status checks
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            var result = new
            {
                Status = "Error",
                Url = uri.ToString(),
                IsSuccessful = false,
                Error = ex.Message,
                ResponseTime = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true, // Still successful operation, just shows error status
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;
        
        return content.Substring(0, maxLength) + $"... (truncated, total length: {content.Length})";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

using System.Text.Json;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class ClaudeToolExecutor
{
    private readonly IWebBrowsingService _webBrowsingService;

    public ClaudeToolExecutor(IWebBrowsingService webBrowsingService)
    {
        _webBrowsingService = webBrowsingService;
    }

    public static List<ClaudeTool> GetAvailableTools()
    {
        return new List<ClaudeTool>
        {
            new ClaudeTool
            {
                Name = "navigate_to_url",
                Description = "Navigate to a specific URL and extract the page content including title, text, and links.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new
                        {
                            type = "string",
                            description = "The URL to navigate to. Must be a valid HTTP or HTTPS URL."
                        },
                        include_links = new
                        {
                            type = "boolean",
                            description = "Whether to include links found on the page. Default is false.",
                            @default = false
                        }
                    },
                    required = new[] { "url" }
                }
            },
            new ClaudeTool
            {
                Name = "search_web",
                Description = "Search the web using Google and return relevant results with titles, URLs, and snippets.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The search query to look for on the web."
                        },
                        max_results = new
                        {
                            type = "integer",
                            description = "Maximum number of search results to return. Default is 5, maximum is 20.",
                            minimum = 1,
                            maximum = 20,
                            @default = 5
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new ClaudeTool
            {
                Name = "extract_text_from_html",
                Description = "Extract clean text content from HTML markup, removing scripts, styles, and navigation elements.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        html = new
                        {
                            type = "string",
                            description = "The HTML content to extract text from."
                        }
                    },
                    required = new[] { "html" }
                }
            }
        };
    }

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            return toolName.ToLowerInvariant() switch
            {
                "navigate_to_url" => await ExecuteNavigateToUrl(parameters, cancellationToken),
                "search_web" => await ExecuteSearchWeb(parameters, cancellationToken),
                "extract_text_from_html" => await ExecuteExtractTextFromHtml(parameters),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteNavigateToUrl(JsonElement parameters, CancellationToken cancellationToken)
    {
        var url = parameters.GetProperty("url").GetString();
        var includeLinks = parameters.TryGetProperty("include_links", out var linksParam) && linksParam.GetBoolean();

        if (string.IsNullOrEmpty(url))
        {
            return JsonSerializer.Serialize(new { error = "URL parameter is required" });
        }

        var result = await _webBrowsingService.GetPageContentAsync(url, includeLinks, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = result.IsSuccessful,
            url = result.Url,
            title = result.Title,
            content = result.TextContent.Length > 8000 ? result.TextContent.Substring(0, 8000) + "..." : result.TextContent,
            links = includeLinks ? result.Links.Take(20).ToArray() : Array.Empty<string>(),
            metadata = result.Metadata,
            error = result.ErrorMessage
        });
    }

    private async Task<string> ExecuteSearchWeb(JsonElement parameters, CancellationToken cancellationToken)
    {
        var query = parameters.GetProperty("query").GetString();
        var maxResults = parameters.TryGetProperty("max_results", out var maxParam) ? maxParam.GetInt32() : 5;

        if (string.IsNullOrEmpty(query))
        {
            return JsonSerializer.Serialize(new { error = "Query parameter is required" });
        }

        var results = await _webBrowsingService.SearchWebAsync(query, Math.Min(maxResults, 20), cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            query,
            results_count = results.Count,
            results = results.Select(r => new
            {
                title = r.Title,
                url = r.Url,
                snippet = r.Snippet,
                source = r.Source
            }).ToArray()
        });
    }

    private async Task<string> ExecuteExtractTextFromHtml(JsonElement parameters)
    {
        var html = parameters.GetProperty("html").GetString();

        if (string.IsNullOrEmpty(html))
        {
            return JsonSerializer.Serialize(new { error = "HTML parameter is required" });
        }

        var extractedText = await _webBrowsingService.ExtractTextFromHtmlAsync(html);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            text_length = extractedText.Length,
            extracted_text = extractedText.Length > 10000 ? extractedText.Substring(0, 10000) + "..." : extractedText
        });
    }
}

public class ClaudeTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new();
}

public class ClaudeToolUse
{
    public string Type { get; set; } = "tool_use";
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
}

public class ClaudeToolResult
{
    public string Type { get; set; } = "tool_result";
    public string ToolUseId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool? IsError { get; set; }
}

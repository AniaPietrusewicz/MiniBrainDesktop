using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class WebBrowsingService : IWebBrowsingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebBrowsingService> _logger;
    private static readonly Regex _linkRegex = new(@"<a\s+(?:[^>]*?\s+)?href=([""'])(.*?)\1", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public WebBrowsingService(HttpClient httpClient, ILogger<WebBrowsingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<WebPageContent> NavigateToUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Navigating to URL: {Url}", url);
        
        try
        {
            if (!IsValidUrl(url))
            {
                return new WebPageContent
                {
                    Url = url,
                    IsSuccessful = false,
                    ErrorMessage = "Invalid URL format"
                };
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new WebPageContent
                {
                    Url = url,
                    IsSuccessful = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";

            if (!contentType.StartsWith("text/html"))
            {
                return new WebPageContent
                {
                    Url = url,
                    TextContent = content.Length > 10000 ? content.Substring(0, 10000) + "..." : content,
                    IsSuccessful = true,
                    Metadata = new Dictionary<string, string> { ["ContentType"] = contentType }
                };
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "No Title";
            var textContent = ExtractTextFromHtml(doc);
            var links = ExtractLinksFromHtml(doc, url);

            return new WebPageContent
            {
                Url = url,
                Title = title,
                TextContent = textContent,
                Links = links,
                IsSuccessful = true,
                Metadata = new Dictionary<string, string> 
                { 
                    ["ContentType"] = contentType,
                    ["StatusCode"] = ((int)response.StatusCode).ToString()
                }
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Request to {Url} was cancelled or timed out", url);
            return new WebPageContent
            {
                Url = url,
                IsSuccessful = false,
                ErrorMessage = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to URL: {Url}", url);
            return new WebPageContent
            {
                Url = url,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<SearchResult>> SearchWebAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching web for: {Query}", query);
        
        var results = new List<SearchResult>();
        
        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var searchUrl = $"https://www.google.com/search?q={encodedQuery}&num={Math.Min(maxResults, 20)}";
            
            var webContent = await NavigateToUrlAsync(searchUrl, cancellationToken);
            
            if (!webContent.IsSuccessful)
            {
                _logger.LogWarning("Failed to perform web search: {Error}", webContent.ErrorMessage);
                return results;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(webContent.TextContent);

            var searchResults = doc.DocumentNode
                .SelectNodes("//div[@class='g']")
                ?.Take(maxResults)
                .Select(node => ParseGoogleSearchResult(node))
                .Where(result => result != null)
                .ToList() ?? new List<SearchResult>();

            results.AddRange(searchResults);
            
            _logger.LogInformation("Found {Count} search results for query: {Query}", results.Count, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing web search for query: {Query}", query);
        }

        return results;
    }

    public async Task<string> ExtractTextFromHtmlAsync(string html)
    {
        return await Task.Run(() =>
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return ExtractTextFromHtml(doc);
        });
    }

    public async Task<List<string>> ExtractLinksFromHtmlAsync(string html, string? baseUrl = null)
    {
        return await Task.Run(() =>
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return ExtractLinksFromHtml(doc, baseUrl);
        });
    }

    public async Task<WebPageContent> GetPageContentAsync(string url, bool includeLinks = false, CancellationToken cancellationToken = default)
    {
        var content = await NavigateToUrlAsync(url, cancellationToken);
        
        if (!includeLinks)
        {
            content.Links.Clear();
        }
        
        return content;
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string ExtractTextFromHtml(HtmlDocument doc)
    {
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script | //style | //noscript | //nav | //footer | //header | //aside");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        var textContent = doc.DocumentNode.InnerText;
        
        textContent = Regex.Replace(textContent, @"\s+", " ");
        textContent = System.Net.WebUtility.HtmlDecode(textContent);
        
        return textContent.Trim();
    }

    private static List<string> ExtractLinksFromHtml(HtmlDocument doc, string? baseUrl = null)
    {
        var links = new List<string>();
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        
        if (linkNodes == null) return links;

        foreach (var linkNode in linkNodes)
        {
            var href = linkNode.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href)) continue;

            if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri))
            {
                links.Add(absoluteUri.ToString());
            }
            else if (!string.IsNullOrEmpty(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                if (Uri.TryCreate(baseUri, href, out var resolvedUri))
                {
                    links.Add(resolvedUri.ToString());
                }
            }
        }

        return links.Distinct().ToList();
    }

    private static SearchResult? ParseGoogleSearchResult(HtmlNode node)
    {
        try
        {
            var titleNode = node.SelectSingleNode(".//h3");
            var linkNode = node.SelectSingleNode(".//a[@href]");
            var snippetNode = node.SelectSingleNode(".//span[contains(@class, 'st')] | .//div[contains(@class, 'VwiC3b')]");

            if (titleNode == null || linkNode == null) return null;

            var title = titleNode.InnerText?.Trim();
            var url = linkNode.GetAttributeValue("href", string.Empty);
            var snippet = snippetNode?.InnerText?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url)) return null;

            return new SearchResult
            {
                Title = System.Net.WebUtility.HtmlDecode(title),
                Url = url,
                Snippet = System.Net.WebUtility.HtmlDecode(snippet),
                Source = "Google Search"
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebBrowsingController : ControllerBase
{
    private readonly IWebBrowsingService _webBrowsingService;

    public WebBrowsingController(IWebBrowsingService webBrowsingService)
    {
        _webBrowsingService = webBrowsingService;
    }

    [HttpPost("navigate")]
    public async Task<IActionResult> NavigateToUrl([FromBody] NavigateRequest request)
    {
        try
        {
            var result = await _webBrowsingService.NavigateToUrlAsync(request.Url);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchWeb([FromBody] SearchRequest request)
    {
        try
        {
            var results = await _webBrowsingService.SearchWebAsync(request.Query, request.MaxResults);
            return Ok(new { query = request.Query, results });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("extract-text")]
    public async Task<IActionResult> ExtractText([FromBody] ExtractTextRequest request)
    {
        try
        {
            var extractedText = await _webBrowsingService.ExtractTextFromHtmlAsync(request.Html);
            return Ok(new { text = extractedText });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class NavigateRequest
{
    public required string Url { get; set; }
    public bool IncludeLinks { get; set; } = false;
}

public class SearchRequest
{
    public required string Query { get; set; }
    public int MaxResults { get; set; } = 5;
}

public class ExtractTextRequest
{
    public required string Html { get; set; }
}

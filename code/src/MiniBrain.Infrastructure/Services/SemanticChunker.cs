using System.Text;
using System.Text.RegularExpressions;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class SemanticChunker : ISemanticChunker
{
    private static readonly Regex _sentenceRegex = new(@"[.!?]+(?:\s|$)", RegexOptions.Compiled);
    private static readonly Regex _paragraphRegex = new(@"\n\s*\n", RegexOptions.Compiled);
    private static readonly Regex _codeBlockRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex _listItemRegex = new(@"^\s*[-*+]\s+", RegexOptions.Compiled | RegexOptions.Multiline);

    public async Task<List<TextChunk>> ChunkTextAsync(string text, int maxChunkSize = 1000, int overlapSize = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        var chunks = new List<TextChunk>();
        var normalizedText = NormalizeText(text);
        
        if (normalizedText.Length <= maxChunkSize)
        {
            chunks.Add(new TextChunk
            {
                Content = normalizedText,
                StartIndex = 0,
                EndIndex = normalizedText.Length,
                ChunkIndex = 0,
                TotalChunks = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["content_type"] = DetectContentType(normalizedText),
                    ["word_count"] = CountWords(normalizedText),
                    ["sentence_count"] = CountSentences(normalizedText)
                }
            });
            return chunks;
        }

        var contentType = DetectContentType(normalizedText);
        
        switch (contentType)
        {
            case "code":
                chunks = await ChunkCodeAsync(normalizedText, maxChunkSize, overlapSize);
                break;
            case "structured":
                chunks = await ChunkStructuredTextAsync(normalizedText, maxChunkSize, overlapSize);
                break;
            default:
                chunks = await ChunkBySemanticBoundariesAsync(normalizedText, maxChunkSize);
                break;
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].ChunkIndex = i;
            chunks[i].TotalChunks = chunks.Count;
        }

        return chunks;
    }

    public async Task<List<TextChunk>> ChunkBySemanticBoundariesAsync(string text, int maxChunkSize = 1000)
    {
        var chunks = new List<TextChunk>();
        var paragraphs = _paragraphRegex.Split(text);
        
        var currentChunk = new StringBuilder();
        var currentStartIndex = 0;
        
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            var trimmedParagraph = paragraph.Trim();
            
            if (currentChunk.Length + trimmedParagraph.Length + 2 > maxChunkSize && currentChunk.Length > 0)
            {
                var chunkContent = currentChunk.ToString().Trim();
                chunks.Add(new TextChunk
                {
                    Content = chunkContent,
                    StartIndex = currentStartIndex,
                    EndIndex = currentStartIndex + chunkContent.Length,
                    Metadata = new Dictionary<string, object>
                    {
                        ["content_type"] = DetectContentType(chunkContent),
                        ["word_count"] = CountWords(chunkContent),
                        ["sentence_count"] = CountSentences(chunkContent)
                    }
                });
                
                currentStartIndex += chunkContent.Length;
                currentChunk.Clear();
            }
            
            if (currentChunk.Length > 0)
                currentChunk.AppendLine();
                
            currentChunk.Append(trimmedParagraph);
        }

        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString().Trim();
            chunks.Add(new TextChunk
            {
                Content = chunkContent,
                StartIndex = currentStartIndex,
                EndIndex = currentStartIndex + chunkContent.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["content_type"] = DetectContentType(chunkContent),
                    ["word_count"] = CountWords(chunkContent),
                    ["sentence_count"] = CountSentences(chunkContent)
                }
            });
        }

        return chunks.Any() ? chunks : await ChunkBySentencesAsync(text, maxChunkSize);
    }

    private async Task<List<TextChunk>> ChunkCodeAsync(string text, int maxChunkSize, int overlapSize)
    {
        var chunks = new List<TextChunk>();
        var codeBlocks = _codeBlockRegex.Matches(text);
        
        if (codeBlocks.Count > 0)
        {
            var lastIndex = 0;
            
            foreach (Match codeBlock in codeBlocks)
            {
                if (codeBlock.Index > lastIndex)
                {
                    var beforeCode = text.Substring(lastIndex, codeBlock.Index - lastIndex);
                    chunks.AddRange(await ChunkBySentencesAsync(beforeCode, maxChunkSize));
                }
                
                chunks.Add(new TextChunk
                {
                    Content = codeBlock.Value,
                    StartIndex = codeBlock.Index,
                    EndIndex = codeBlock.Index + codeBlock.Length,
                    Metadata = new Dictionary<string, object>
                    {
                        ["content_type"] = "code_block",
                        ["language"] = ExtractCodeLanguage(codeBlock.Value)
                    }
                });
                
                lastIndex = codeBlock.Index + codeBlock.Length;
            }
            
            if (lastIndex < text.Length)
            {
                var afterCode = text.Substring(lastIndex);
                chunks.AddRange(await ChunkBySentencesAsync(afterCode, maxChunkSize));
            }
        }
        else
        {
            chunks.AddRange(await ChunkBySentencesAsync(text, maxChunkSize));
        }

        return chunks;
    }

    private async Task<List<TextChunk>> ChunkStructuredTextAsync(string text, int maxChunkSize, int overlapSize)
    {
        var chunks = new List<TextChunk>();
        var lines = text.Split('\n');
        var currentChunk = new StringBuilder();
        var currentStartIndex = 0;
        
        foreach (var line in lines)
        {
            if (_listItemRegex.IsMatch(line) || line.Trim().StartsWith("#"))
            {
                if (currentChunk.Length > 0 && currentChunk.Length + line.Length > maxChunkSize)
                {
                    var chunkContent = currentChunk.ToString().Trim();
                    chunks.Add(new TextChunk
                    {
                        Content = chunkContent,
                        StartIndex = currentStartIndex,
                        EndIndex = currentStartIndex + chunkContent.Length,
                        Metadata = new Dictionary<string, object>
                        {
                            ["content_type"] = "structured",
                            ["structure_type"] = DetectStructureType(chunkContent)
                        }
                    });
                    
                    currentStartIndex += chunkContent.Length;
                    currentChunk.Clear();
                }
            }
            
            if (currentChunk.Length > 0)
                currentChunk.AppendLine();
            currentChunk.Append(line);
        }

        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString().Trim();
            chunks.Add(new TextChunk
            {
                Content = chunkContent,
                StartIndex = currentStartIndex,
                EndIndex = currentStartIndex + chunkContent.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["content_type"] = "structured",
                    ["structure_type"] = DetectStructureType(chunkContent)
                }
            });
        }

        return chunks;
    }

    private async Task<List<TextChunk>> ChunkBySentencesAsync(string text, int maxChunkSize)
    {
        var chunks = new List<TextChunk>();
        var sentences = _sentenceRegex.Split(text);
        var currentChunk = new StringBuilder();
        var currentStartIndex = 0;
        
        foreach (var sentence in sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                continue;

            var trimmedSentence = sentence.Trim();
            
            if (currentChunk.Length + trimmedSentence.Length + 1 > maxChunkSize && currentChunk.Length > 0)
            {
                var chunkContent = currentChunk.ToString().Trim();
                chunks.Add(new TextChunk
                {
                    Content = chunkContent,
                    StartIndex = currentStartIndex,
                    EndIndex = currentStartIndex + chunkContent.Length,
                    Metadata = new Dictionary<string, object>
                    {
                        ["content_type"] = "text",
                        ["word_count"] = CountWords(chunkContent),
                        ["sentence_count"] = CountSentences(chunkContent)
                    }
                });
                
                currentStartIndex += chunkContent.Length;
                currentChunk.Clear();
            }
            
            if (currentChunk.Length > 0)
                currentChunk.Append(" ");
            currentChunk.Append(trimmedSentence);
        }

        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString().Trim();
            chunks.Add(new TextChunk
            {
                Content = chunkContent,
                StartIndex = currentStartIndex,
                EndIndex = currentStartIndex + chunkContent.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["content_type"] = "text",
                    ["word_count"] = CountWords(chunkContent),
                    ["sentence_count"] = CountSentences(chunkContent)
                }
            });
        }

        return chunks;
    }

    private static string NormalizeText(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

    private static string DetectContentType(string text)
    {
        if (_codeBlockRegex.IsMatch(text) || text.Contains("```") || text.Contains("function") || text.Contains("class"))
            return "code";
            
        if (_listItemRegex.IsMatch(text) || text.Contains("#") || text.Contains("* ") || text.Contains("- "))
            return "structured";
            
        return "text";
    }

    private static string ExtractCodeLanguage(string codeBlock)
    {
        var match = Regex.Match(codeBlock, @"```(\w+)");
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    private static string DetectStructureType(string text)
    {
        if (text.Contains("#")) return "markdown";
        if (_listItemRegex.IsMatch(text)) return "list";
        return "structured";
    }

    private static int CountWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int CountSentences(string text)
    {
        return _sentenceRegex.Matches(text).Count;
    }
}

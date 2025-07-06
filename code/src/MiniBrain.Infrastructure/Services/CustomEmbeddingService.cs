using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniBrain.Core.Interfaces;

namespace MiniBrain.Infrastructure.Services;

public class CustomEmbeddingService : IEmbeddingService
{
    private readonly ILogger<CustomEmbeddingService> _logger;
    private readonly ISemanticChunker _semanticChunker;
    private readonly ConcurrentDictionary<string, float[]> _embeddingCache;
    private readonly ConcurrentDictionary<string, double> _idfCache;
    private readonly object _lockObject = new();
    
    // ⚠️ ⚠️ ⚠️ CRITICAL WARNING - DO NOT TOUCH THIS PARAMETER! ⚠️ ⚠️ ⚠️
    // This dimension count MUST be loaded from Qdrant.VectorSize configuration
    // to ensure perfect synchronization with Qdrant vector database.
    // Changing this dimension will break vector compatibility and cause system failure!
    // This value MUST always match the Qdrant collection vector size (512).
    // DO NOT make this a const again - it must stay synchronized with config!
    private readonly int _embeddingDimension;
    private const int MIN_DOCUMENT_FREQUENCY = 2;
    private const double SMOOTHING_FACTOR = 0.1;
    
    private static readonly Regex _wordRegex = new(@"\b\w+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "can", "this", "that", "these", "those", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its", "our", "their"
    };

    public CustomEmbeddingService(ILogger<CustomEmbeddingService> logger, ISemanticChunker semanticChunker, IConfiguration configuration)
    {
        _logger = logger;
        _semanticChunker = semanticChunker;
        _embeddingCache = new ConcurrentDictionary<string, float[]>();
        _idfCache = new ConcurrentDictionary<string, double>();
        
        // ⚠️ ⚠️ ⚠️ CRITICAL WARNING - DO NOT TOUCH THIS CONFIGURATION! ⚠️ ⚠️ ⚠️
        // Load embedding dimension from same config as Qdrant to ensure perfect sync.
        // This MUST always match Qdrant.VectorSize (512 dimensions).
        // Any change here will break vector compatibility and cause system failure!
        var qdrantConfig = configuration.GetSection("Qdrant");
        _embeddingDimension = int.Parse(qdrantConfig["VectorSize"] ?? "512");
        
        _logger.LogInformation("CustomEmbeddingService initialized with {Dimensions} dimensions (from Qdrant config)", _embeddingDimension);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[_embeddingDimension];

        var normalizedText = NormalizeText(text);
        var cacheKey = GenerateCacheKey(normalizedText);
        
        if (_embeddingCache.TryGetValue(cacheKey, out var cachedEmbedding))
            return cachedEmbedding;

        try
        {
            var embedding = await ComputeEmbeddingAsync(normalizedText);
            _embeddingCache.TryAdd(cacheKey, embedding);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text: {Text}", text[..Math.Min(100, text.Length)]);
            return new float[_embeddingDimension];
        }
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();
        
        var tasks = texts.Select(async text =>
        {
            try
            {
                return await GenerateEmbeddingAsync(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch embedding generation");
                return new float[_embeddingDimension];
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<List<TextChunk>> ChunkTextAsync(string text, int maxChunkSize = 1000, int overlapSize = 200)
    {
        return await _semanticChunker.ChunkTextAsync(text, maxChunkSize, overlapSize);
    }

    private async Task<float[]> ComputeEmbeddingAsync(string text)
    {
        var words = ExtractWords(text);
        var wordFrequencies = ComputeWordFrequencies(words);
        var tfIdfVector = ComputeTfIdfVector(wordFrequencies, text);
        
        var semanticFeatures = await ComputeSemanticFeaturesAsync(text);
        var syntacticFeatures = ComputeSyntacticFeatures(text);
        var positionFeatures = ComputePositionFeatures(words);
        
        var combinedVector = CombineFeatures(tfIdfVector, semanticFeatures, syntacticFeatures, positionFeatures);
        
        return NormalizeVector(combinedVector);
    }

    private List<string> ExtractWords(string text)
    {
        var matches = _wordRegex.Matches(text.ToLowerInvariant());
        return matches
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(word => !_stopWords.Contains(word) && word.Length > 2)
            .ToList();
    }

    private Dictionary<string, double> ComputeWordFrequencies(List<string> words)
    {
        var frequencies = new Dictionary<string, double>();
        var totalWords = words.Count;
        
        foreach (var word in words)
        {
            frequencies[word] = frequencies.GetValueOrDefault(word, 0) + 1;
        }
        
        foreach (var key in frequencies.Keys.ToList())
        {
            frequencies[key] = frequencies[key] / totalWords;
        }
        
        return frequencies;
    }

    private float[] ComputeTfIdfVector(Dictionary<string, double> termFrequencies, string document)
    {
        var vector = new float[_embeddingDimension / 4];
        var index = 0;
        
        foreach (var kvp in termFrequencies.Take(vector.Length))
        {
            var tf = kvp.Value;
            var idf = GetInverseDocumentFrequency(kvp.Key);
            vector[index++] = (float)(tf * idf);
        }
        
        return vector;
    }

    private async Task<float[]> ComputeSemanticFeaturesAsync(string text)
    {
        var features = new float[_embeddingDimension / 4];
        var words = ExtractWords(text);
        
        if (!words.Any())
            return features;

        var wordVectors = words.Select(word => ComputeWordVector(word)).ToArray();
        
        for (int i = 0; i < features.Length && i < wordVectors.Length; i++)
        {
            features[i] = wordVectors.Select(v => v[i % v.Length]).Average();
        }
        
        var contextualFeatures = ComputeContextualFeatures(words);
        for (int i = 0; i < Math.Min(contextualFeatures.Length, features.Length); i++)
        {
            features[i] = (features[i] + contextualFeatures[i]) / 2;
        }
        
        return features;
    }

    private float[] ComputeWordVector(string word)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(word));
        var vector = new float[64];
        
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (hash[i % hash.Length] - 128) / 128f;
        }
        
        var length = Math.Sqrt(vector.Sum(v => v * v));
        if (length > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= (float)length;
            }
        }
        
        return vector;
    }

    private float[] ComputeContextualFeatures(List<string> words)
    {
        var features = new float[_embeddingDimension / 4];
        
        if (words.Count < 2)
            return features;

        var bigramFeatures = ComputeBigramFeatures(words);
        var positionFeatures = ComputeWordPositionFeatures(words);
        
        for (int i = 0; i < Math.Min(features.Length, bigramFeatures.Length); i++)
        {
            features[i] = bigramFeatures[i];
        }
        
        var offset = bigramFeatures.Length;
        for (int i = 0; i < Math.Min(features.Length - offset, positionFeatures.Length); i++)
        {
            features[offset + i] = positionFeatures[i];
        }
        
        return features;
    }

    private float[] ComputeBigramFeatures(List<string> words)
    {
        var features = new float[32];
        var bigramCounts = new Dictionary<string, int>();
        
        for (int i = 0; i < words.Count - 1; i++)
        {
            var bigram = $"{words[i]}_{words[i + 1]}";
            bigramCounts[bigram] = bigramCounts.GetValueOrDefault(bigram, 0) + 1;
        }
        
        var topBigrams = bigramCounts.OrderByDescending(kvp => kvp.Value).Take(features.Length);
        var index = 0;
        
        foreach (var kvp in topBigrams)
        {
            if (index >= features.Length) break;
            features[index++] = (float)kvp.Value / words.Count;
        }
        
        return features;
    }

    private float[] ComputeWordPositionFeatures(List<string> words)
    {
        var features = new float[32];
        
        if (!words.Any())
            return features;

        var positionWeights = new Dictionary<string, List<float>>();
        
        for (int i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var position = (float)i / words.Count;
            
            if (!positionWeights.ContainsKey(word))
                positionWeights[word] = new List<float>();
            
            positionWeights[word].Add(position);
        }
        
        var topWords = positionWeights.OrderByDescending(kvp => kvp.Value.Count).Take(features.Length);
        var index = 0;
        
        foreach (var kvp in topWords)
        {
            if (index >= features.Length) break;
            features[index++] = kvp.Value.Average();
        }
        
        return features;
    }

    private float[] ComputeSyntacticFeatures(string text)
    {
        var features = new float[_embeddingDimension / 4];
        
        features[0] = CountSentences(text);
        features[1] = CountWords(text);
        features[2] = CountCharacters(text);
        features[3] = ComputeAverageWordLength(text);
        features[4] = ComputeReadabilityScore(text);
        features[5] = CountPunctuationMarks(text);
        features[6] = CountCapitalLetters(text);
        features[7] = CountNumbers(text);
        
        var linguisticFeatures = ComputeLinguisticFeatures(text);
        for (int i = 8; i < Math.Min(features.Length, linguisticFeatures.Length + 8); i++)
        {
            features[i] = linguisticFeatures[i - 8];
        }
        
        return features;
    }

    private float[] ComputeLinguisticFeatures(string text)
    {
        var features = new float[24];
        
        features[0] = CountPattern(text, @"[.!?]");
        features[1] = CountPattern(text, @"[,;:]");
        features[2] = CountPattern(text, @"[""']");
        features[3] = CountPattern(text, @"\b\w+ing\b");
        features[4] = CountPattern(text, @"\b\w+ed\b");
        features[5] = CountPattern(text, @"\b\w+ly\b");
        features[6] = CountPattern(text, @"\b\w+tion\b");
        features[7] = CountPattern(text, @"\b\w+ness\b");
        features[8] = CountPattern(text, @"\b(very|quite|rather|extremely)\b");
        features[9] = CountPattern(text, @"\b(not|no|never|none)\b");
        features[10] = CountPattern(text, @"\b(and|or|but|however|therefore)\b");
        features[11] = CountPattern(text, @"\b(I|you|he|she|it|we|they)\b");
        features[12] = CountPattern(text, @"\b(this|that|these|those)\b");
        features[13] = CountPattern(text, @"\b(can|could|may|might|must|should|will|would)\b");
        features[14] = CountPattern(text, @"\b(in|on|at|by|for|with|from|to|of)\b");
        features[15] = CountPattern(text, @"\b(what|when|where|why|how|who)\b");
        
        var avgSentenceLength = ComputeAverageSentenceLength(text);
        features[16] = avgSentenceLength;
        features[17] = ComputeVarianceInSentenceLength(text, avgSentenceLength);
        features[18] = ComputeLexicalDiversity(text);
        features[19] = ComputeComplexityScore(text);
        features[20] = ComputeCoherenceScore(text);
        features[21] = ComputeEmotionalTone(text);
        features[22] = ComputeFormalityScore(text);
        features[23] = ComputeSubjectivityScore(text);
        
        return features;
    }

    private float[] ComputePositionFeatures(List<string> words)
    {
        var features = new float[_embeddingDimension / 4];
        
        if (!words.Any())
            return features;

        var firstWords = words.Take(Math.Min(5, words.Count)).ToList();
        var lastWords = words.Skip(Math.Max(0, words.Count - 5)).ToList();
        
        for (int i = 0; i < Math.Min(features.Length / 2, firstWords.Count); i++)
        {
            var wordVector = ComputeWordVector(firstWords[i]);
            features[i] = wordVector.Take(8).Average();
        }
        
        var offset = features.Length / 2;
        for (int i = 0; i < Math.Min(features.Length - offset, lastWords.Count); i++)
        {
            var wordVector = ComputeWordVector(lastWords[i]);
            features[offset + i] = wordVector.Take(8).Average();
        }
        
        return features;
    }

    private float[] CombineFeatures(float[] tfIdf, float[] semantic, float[] syntactic, float[] positional)
    {
        var combined = new float[_embeddingDimension];
        
        Array.Copy(tfIdf, 0, combined, 0, Math.Min(tfIdf.Length, _embeddingDimension / 4));
        Array.Copy(semantic, 0, combined, _embeddingDimension / 4, Math.Min(semantic.Length, _embeddingDimension / 4));
        Array.Copy(syntactic, 0, combined, _embeddingDimension / 2, Math.Min(syntactic.Length, _embeddingDimension / 4));
        Array.Copy(positional, 0, combined, 3 * _embeddingDimension / 4, Math.Min(positional.Length, _embeddingDimension / 4));
        
        return combined;
    }

    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        
        if (magnitude < 1e-10)
            return vector;
        
        return vector.Select(v => v / (float)magnitude).ToArray();
    }

    private double GetInverseDocumentFrequency(string term)
    {
        if (_idfCache.TryGetValue(term, out var idf))
            return idf;
        
        var calculatedIdf = Math.Log(1000.0 / (MIN_DOCUMENT_FREQUENCY + SMOOTHING_FACTOR));
        _idfCache.TryAdd(term, calculatedIdf);
        return calculatedIdf;
    }

    private static string NormalizeText(string text)
    {
        return text.ToLowerInvariant().Trim();
    }

    private static string GenerateCacheKey(string text)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hash);
    }

    private static float CountSentences(string text)
    {
        return Regex.Matches(text, @"[.!?]+").Count;
    }

    private static float CountWords(string text)
    {
        return _wordRegex.Matches(text).Count;
    }

    private static float CountCharacters(string text)
    {
        return text.Length;
    }

    private static float ComputeAverageWordLength(string text)
    {
        var words = _wordRegex.Matches(text);
        if (words.Count == 0) return 0;
        
        return (float)words.Cast<Match>().Select(m => m.Length).Average();
    }

    private static float ComputeReadabilityScore(string text)
    {
        var sentences = CountSentences(text);
        var words = CountWords(text);
        var syllables = CountSyllables(text);
        
        if (sentences == 0 || words == 0) return 0;
        
        return 206.835f - (1.015f * (words / sentences)) - (84.6f * (syllables / words));
    }

    private static float CountSyllables(string text)
    {
        var words = _wordRegex.Matches(text);
        var totalSyllables = 0;
        
        foreach (Match word in words)
        {
            totalSyllables += EstimateSyllables(word.Value);
        }
        
        return totalSyllables;
    }

    private static int EstimateSyllables(string word)
    {
        var vowels = "aeiouy";
        var syllableCount = 0;
        var previousWasVowel = false;
        
        foreach (char c in word.ToLowerInvariant())
        {
            var isVowel = vowels.Contains(c);
            if (isVowel && !previousWasVowel)
            {
                syllableCount++;
            }
            previousWasVowel = isVowel;
        }
        
        if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase))
        {
            syllableCount--;
        }
        
        return Math.Max(1, syllableCount);
    }

    private static float CountPunctuationMarks(string text)
    {
        return Regex.Matches(text, @"[^\w\s]").Count;
    }

    private static float CountCapitalLetters(string text)
    {
        return text.Count(char.IsUpper);
    }

    private static float CountNumbers(string text)
    {
        return Regex.Matches(text, @"\d+").Count;
    }

    private static float CountPattern(string text, string pattern)
    {
        return Regex.Matches(text, pattern, RegexOptions.IgnoreCase).Count;
    }

    private static float ComputeAverageSentenceLength(string text)
    {
        var sentences = CountSentences(text);
        var words = CountWords(text);
        
        return sentences > 0 ? words / sentences : 0;
    }

    private static float ComputeVarianceInSentenceLength(string text, float averageLength)
    {
        var sentences = Regex.Split(text, @"[.!?]+");
        var lengths = sentences.Select(s => (float)_wordRegex.Matches(s).Count).ToArray();
        
        if (lengths.Length <= 1) return 0;
        
        var variance = lengths.Select(l => (l - averageLength) * (l - averageLength)).Average();
        return (float)Math.Sqrt(variance);
    }

    private static float ComputeLexicalDiversity(string text)
    {
        var words = _wordRegex.Matches(text).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
        var uniqueWords = words.Distinct().Count();
        
        return words.Count > 0 ? (float)uniqueWords / words.Count : 0;
    }

    private static float ComputeComplexityScore(string text)
    {
        var avgWordLength = ComputeAverageWordLength(text);
        var avgSentenceLength = ComputeAverageSentenceLength(text);
        var punctuationDensity = CountPunctuationMarks(text) / Math.Max(1, CountWords(text));
        
        return (avgWordLength + avgSentenceLength + punctuationDensity) / 3;
    }

    private static float ComputeCoherenceScore(string text)
    {
        var sentences = Regex.Split(text, @"[.!?]+");
        if (sentences.Length <= 1) return 1.0f;
        
        var coherenceScore = 0f;
        for (int i = 0; i < sentences.Length - 1; i++)
        {
            var currentWords = _wordRegex.Matches(sentences[i]).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToHashSet();
            var nextWords = _wordRegex.Matches(sentences[i + 1]).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToHashSet();
            
            var intersection = currentWords.Intersect(nextWords).Count();
            var union = currentWords.Union(nextWords).Count();
            
            coherenceScore += union > 0 ? (float)intersection / union : 0;
        }
        
        return coherenceScore / (sentences.Length - 1);
    }

    private static float ComputeEmotionalTone(string text)
    {
        var positiveWords = new[] { "good", "great", "excellent", "wonderful", "amazing", "fantastic", "beautiful", "love", "happy", "joy" };
        var negativeWords = new[] { "bad", "terrible", "awful", "horrible", "hate", "sad", "angry", "fear", "worry", "problem" };
        
        var words = _wordRegex.Matches(text).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
        
        var positiveCount = words.Count(w => positiveWords.Contains(w));
        var negativeCount = words.Count(w => negativeWords.Contains(w));
        
        return words.Count > 0 ? (float)(positiveCount - negativeCount) / words.Count : 0;
    }

    private static float ComputeFormalityScore(string text)
    {
        var formalWords = new[] { "furthermore", "moreover", "however", "nevertheless", "consequently", "therefore", "subsequently", "accordingly" };
        var informalWords = new[] { "yeah", "ok", "gonna", "wanna", "kinda", "sorta", "dunno", "anyways" };
        
        var words = _wordRegex.Matches(text).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
        
        var formalCount = words.Count(w => formalWords.Contains(w));
        var informalCount = words.Count(w => informalWords.Contains(w));
        
        return words.Count > 0 ? (float)(formalCount - informalCount) / words.Count : 0;
    }

    private static float ComputeSubjectivityScore(string text)
    {
        var subjectiveWords = new[] { "think", "feel", "believe", "opinion", "seems", "appears", "probably", "maybe", "perhaps", "possibly" };
        var objectiveWords = new[] { "fact", "data", "research", "study", "evidence", "proven", "demonstrated", "observed", "measured", "documented" };
        
        var words = _wordRegex.Matches(text).Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
        
        var subjectiveCount = words.Count(w => subjectiveWords.Contains(w));
        var objectiveCount = words.Count(w => objectiveWords.Contains(w));
        
        return words.Count > 0 ? (float)(subjectiveCount - objectiveCount) / words.Count : 0;
    }
}

using Microsoft.Extensions.Logging;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MiniBrain.Infrastructure.Services;

public class QdrantClientWrapper : IQdrantClient
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantClientWrapper> _logger;

    public QdrantClientWrapper(string baseUrl, ILogger<QdrantClientWrapper> logger)
    {
        var uri = new Uri(baseUrl);
        // ⚠️ CRITICAL FIX: Qdrant gRPC client must use gRPC port (6334), not HTTP port (6333)!
        // BaseUrl is HTTP API (6333) but gRPC client needs gRPC port (6334)
        var grpcPort = uri.Port == 6333 ? 6334 : uri.Port + 1;
        _client = new QdrantClient(uri.Host, grpcPort);
        _logger = logger;
    }

    public async Task CreateCollectionAsync(MiniBrain.Core.Models.CollectionConfig config)
    {
        try
        {
            // ⚠️ ⚠️ ⚠️ CRITICAL WARNING - DO NOT TOUCH THIS VECTOR SIZE! ⚠️ ⚠️ ⚠️
            // This vector size comes from config.VectorSize which is loaded from Qdrant.VectorSize
            // and MUST match CustomEmbeddingService embedding dimension (512).
            // Any change will break vector compatibility and cause system failure!
            var vectorParams = new VectorParams
            {
                Size = (uint)config.VectorSize,
                Distance = config.Distance.ToLowerInvariant() switch
                {
                    "cosine" => Distance.Cosine,
                    "euclidean" => Distance.Euclid,
                    "dot" => Distance.Dot,
                    _ => Distance.Cosine
                }
            };

            await _client.CreateCollectionAsync(config.Name, vectorParams);
            _logger.LogInformation("Created Qdrant collection: {CollectionName}", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection: {CollectionName}", config.Name);
            throw;
        }
    }

    public async Task UpsertAsync(string collection, List<PointStruct> points)
    {
        try
        {
            await _client.UpsertAsync(collection, points);
            _logger.LogDebug("Upserted {Count} points to collection: {Collection}", points.Count, collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert points to collection: {Collection}", collection);
            throw;
        }
    }

    public async Task<List<ScoredPoint>> SearchAsync(string collection, float[] vector, int limit)
    {
        try
        {
            var result = await _client.SearchAsync(collection, vector, limit: (uint)limit);
            _logger.LogDebug("Search returned {Count} results from collection: {Collection}", result.Count, collection);
            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search collection: {Collection}", collection);
            throw;
        }
    }

    public async Task<List<Record>> RetrieveAsync(string collection, List<string> ids)
    {
        try
        {
            var pointIds = ids.Select(id => new PointId { Uuid = id }).ToList();
            var result = await _client.RetrieveAsync(collection, pointIds);
            
            var records = result.Select(point => new Record
            {
                Id = point.Id.Uuid,
                Payload = point.Payload.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                Vector = point.Vectors?.Vector?.Data?.ToArray() ?? Array.Empty<float>()
            }).ToList();

            _logger.LogDebug("Retrieved {Count} records from collection: {Collection}", records.Count, collection);
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve records from collection: {Collection}", collection);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string collection, List<string> ids)
    {
        try
        {
            // TODO: Implement proper deletion when the correct API is determined
            _logger.LogWarning("DeleteAsync not yet implemented - returning false for {Count} points in collection: {Collection}", ids.Count, collection);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete points from collection: {Collection}", collection);
            return false;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

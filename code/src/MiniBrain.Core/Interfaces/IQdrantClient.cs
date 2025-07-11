using MiniBrain.Core.Models;
using Qdrant.Client.Grpc;

namespace MiniBrain.Core.Interfaces;

public interface IQdrantClient
{
    Task CreateCollectionAsync(MiniBrain.Core.Models.CollectionConfig config);
    Task UpsertAsync(string collection, List<PointStruct> points);
    Task<List<ScoredPoint>> SearchAsync(string collection, float[] vector, int limit);
    Task<List<Record>> RetrieveAsync(string collection, List<string> ids);
    Task<bool> DeleteAsync(string collection, List<string> ids);
}

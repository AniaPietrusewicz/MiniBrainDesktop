using MiniBrain.Core.Models;

namespace MiniBrain.Core.Interfaces;

public interface IClaudeApiService
{
    Task<string> SendMessageAsync(string message, string? systemPrompt = null, CancellationToken cancellationToken = default);
    Task<string> SendMessagesAsync(IEnumerable<Message> messages, string? systemPrompt = null, CancellationToken cancellationToken = default);
}

public interface IAgentService
{
    Task<Agent> CreateAgentAsync(string name, string description, string instructions);
    Task<Agent?> GetAgentAsync(Guid id);
    Task<List<Agent>> GetAllAgentsAsync();
    Task<Agent> UpdateAgentAsync(Agent agent);
    Task<bool> DeleteAgentAsync(Guid id);
}

public interface IWorkflowService
{
    Task<Workflow> CreateWorkflowAsync(Guid agentId, string name, string description);
    Task<Workflow?> GetWorkflowAsync(Guid id);
    Task<List<Workflow>> GetWorkflowsByAgentAsync(Guid agentId);
    Task<WorkflowExecution> ExecuteWorkflowAsync(Guid workflowId, string inputData);
    Task<WorkflowExecution?> GetWorkflowExecutionAsync(Guid executionId);
}

public interface IGoalService
{
    Task<Goal> CreateGoalAsync(Guid agentId, string title, string description, int priority = 5);
    Task<Goal?> GetGoalAsync(Guid id);
    Task<List<Goal>> GetGoalsByAgentAsync(Guid agentId);
    Task<Goal> UpdateGoalStatusAsync(Guid goalId, GoalStatus status);
    Task<Goal> AddGoalStepAsync(Guid goalId, string description, int orderIndex);
    Task<bool> CompleteGoalStepAsync(Guid stepId);
}

public interface IConversationService
{
    Task<ConversationContext> CreateConversationAsync(Guid agentId, string sessionId);
    Task<ConversationContext?> GetConversationAsync(string sessionId);
    Task<Message> AddMessageAsync(string sessionId, string role, string content);
    Task<List<Message>> GetConversationHistoryAsync(string sessionId, int limit = 50);
    Task<string> ProcessMessageAsync(string sessionId, string userMessage);
}

public interface IVectorSearchService
{
    Task<bool> StoreVectorAsync(Guid id, string text, Dictionary<string, object>? metadata = null);
    Task<List<VectorSearchResult>> SearchAsync(string query, int limit = 10, double threshold = 0.7);
    Task<bool> DeleteVectorAsync(Guid id);
}

public class VectorSearchResult
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

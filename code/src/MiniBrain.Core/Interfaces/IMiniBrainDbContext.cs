using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Models;

namespace MiniBrain.Core.Interfaces;

public interface IMiniBrainDbContext
{
    DbSet<Agent> Agents { get; set; }
    DbSet<Workflow> Workflows { get; set; }
    DbSet<WorkflowStep> WorkflowSteps { get; set; }
    DbSet<WorkflowExecution> WorkflowExecutions { get; set; }
    DbSet<StepExecution> StepExecutions { get; set; }
    DbSet<Goal> Goals { get; set; }
    DbSet<GoalStep> GoalSteps { get; set; }
    DbSet<ConversationContext> ConversationContexts { get; set; }
    DbSet<Message> Messages { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

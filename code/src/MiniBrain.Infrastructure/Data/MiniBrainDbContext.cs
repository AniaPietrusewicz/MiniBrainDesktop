using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Data;

public class MiniBrainDbContext : DbContext, IMiniBrainDbContext
{
    public MiniBrainDbContext(DbContextOptions<MiniBrainDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowExecution> WorkflowExecutions { get; set; }
    public DbSet<StepExecution> StepExecutions { get; set; }
    public DbSet<Goal> Goals { get; set; }
    public DbSet<GoalStep> GoalSteps { get; set; }
    public DbSet<ConversationContext> ConversationContexts { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Instructions).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.Agent)
                  .WithMany(e => e.Workflows)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ActionType).IsRequired();
            entity.Property(e => e.Parameters).HasDefaultValue("{}");
            entity.HasOne(e => e.Workflow)
                  .WithMany(e => e.Steps)
                  .HasForeignKey(e => e.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.InputData).HasDefaultValue("{}");
            entity.Property(e => e.OutputData).HasDefaultValue("{}");
            entity.HasOne(e => e.Workflow)
                  .WithMany(e => e.Executions)
                  .HasForeignKey(e => e.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StepExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.InputData).HasDefaultValue("{}");
            entity.Property(e => e.OutputData).HasDefaultValue("{}");
            entity.HasOne(e => e.WorkflowStep)
                  .WithMany(e => e.Executions)
                  .HasForeignKey(e => e.WorkflowStepId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowExecution)
                  .WithMany(e => e.StepExecutions)
                  .HasForeignKey(e => e.WorkflowExecutionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasOne(e => e.Agent)
                  .WithMany(e => e.Goals)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Goal)
                  .WithMany(e => e.Steps)
                  .HasForeignKey(e => e.GoalId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationContext>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContextData).HasDefaultValue("{}");
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasOne(e => e.Agent)
                  .WithMany(e => e.ConversationContexts)
                  .HasForeignKey(e => e.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Configure complex properties as JSON
            entity.Property(e => e.Tags)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new List<string>()
                  );
                  
            entity.Property(e => e.Metadata)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions)null) ?? new Dictionary<string, object>()
                  );
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasOne(e => e.ConversationContext)
                  .WithMany(e => e.Messages)
                  .HasForeignKey(e => e.ConversationContextId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class GoalService : IGoalService
{
    private readonly IMiniBrainDbContext _context;

    public GoalService(IMiniBrainDbContext context)
    {
        _context = context;
    }

    public async Task<Goal> CreateGoalAsync(Guid agentId, string title, string description, int priority = 5)
    {
        var goal = new Goal
        {
            AgentId = agentId,
            Title = title,
            Description = description,
            Priority = priority
        };

        _context.Goals.Add(goal);
        await _context.SaveChangesAsync();

        return goal;
    }

    public async Task<Goal?> GetGoalAsync(Guid id)
    {
        return await _context.Goals
            .Include(g => g.Agent)
            .Include(g => g.Steps.OrderBy(s => s.OrderIndex))
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<List<Goal>> GetGoalsByAgentAsync(Guid agentId)
    {
        return await _context.Goals
            .Include(g => g.Steps)
            .Where(g => g.AgentId == agentId)
            .OrderByDescending(g => g.Priority)
            .ThenBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Goal> UpdateGoalStatusAsync(Guid goalId, GoalStatus status)
    {
        var goal = await _context.Goals.FindAsync(goalId);
        if (goal == null)
            throw new ArgumentException($"Goal {goalId} not found");

        goal.Status = status;
        
        if (status == GoalStatus.Completed)
        {
            goal.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return goal;
    }

    public async Task<Goal> AddGoalStepAsync(Guid goalId, string description, int orderIndex)
    {
        var goal = await GetGoalAsync(goalId);
        if (goal == null)
            throw new ArgumentException($"Goal {goalId} not found");

        var step = new GoalStep
        {
            GoalId = goalId,
            Description = description,
            OrderIndex = orderIndex
        };

        _context.GoalSteps.Add(step);
        await _context.SaveChangesAsync();

        goal.Steps.Add(step);
        return goal;
    }

    public async Task<bool> CompleteGoalStepAsync(Guid stepId)
    {
        var step = await _context.GoalSteps
            .Include(s => s.Goal)
                .ThenInclude(g => g.Steps)
            .FirstOrDefaultAsync(s => s.Id == stepId);
        
        if (step == null)
            return false;

        step.IsCompleted = true;
        step.CompletedAt = DateTime.UtcNow;

        var allStepsCompleted = step.Goal.Steps.All(s => s.IsCompleted);
        if (allStepsCompleted && step.Goal.Status == GoalStatus.Active)
        {
            step.Goal.Status = GoalStatus.Completed;
            step.Goal.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}

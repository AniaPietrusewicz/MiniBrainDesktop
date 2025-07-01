using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Interfaces;
using MiniBrain.Core.Models;

namespace MiniBrain.Infrastructure.Services;

public class AgentService : IAgentService
{
    private readonly IMiniBrainDbContext _context;

    public AgentService(IMiniBrainDbContext context)
    {
        _context = context;
    }

    public async Task<Agent> CreateAgentAsync(string name, string description, string instructions)
    {
        var agent = new Agent
        {
            Name = name,
            Description = description,
            Instructions = instructions
        };

        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        return agent;
    }

    public async Task<Agent?> GetAgentAsync(Guid id)
    {
        return await _context.Agents
            .Include(a => a.Workflows)
            .Include(a => a.Goals)
            .Include(a => a.ConversationContexts)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Agent>> GetAllAgentsAsync()
    {
        return await _context.Agents
            .Include(a => a.Workflows)
            .Include(a => a.Goals)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Agent> UpdateAgentAsync(Agent agent)
    {
        agent.UpdatedAt = DateTime.UtcNow;
        _context.Agents.Update(agent);
        await _context.SaveChangesAsync();
        return agent;
    }

    public async Task<bool> DeleteAgentAsync(Guid id)
    {
        var agent = await _context.Agents.FindAsync(id);
        if (agent == null)
            return false;

        _context.Agents.Remove(agent);
        await _context.SaveChangesAsync();
        return true;
    }
}

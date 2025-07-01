using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Models;
using MiniBrain.Infrastructure.Data;
using MiniBrain.Infrastructure.Services;

namespace MiniBrain.Tests;

public class AgentServiceTests
{
    [Fact]
    public async Task CreateAgent_ShouldCreateNewAgent()
    {
        var options = new DbContextOptionsBuilder<MiniBrainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new MiniBrainDbContext(options);
        var service = new AgentService(context);

        var agent = await service.CreateAgentAsync("Test Agent", "Test Description", "Test Instructions");

        Assert.NotNull(agent);
        Assert.Equal("Test Agent", agent.Name);
        Assert.Equal("Test Description", agent.Description);
        Assert.Equal("Test Instructions", agent.Instructions);
        Assert.True(agent.IsActive);
    }

    [Fact]
    public async Task GetAgent_ShouldReturnAgent_WhenExists()
    {
        var options = new DbContextOptionsBuilder<MiniBrainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new MiniBrainDbContext(options);
        var service = new AgentService(context);

        var createdAgent = await service.CreateAgentAsync("Test Agent", "Test Description", "Test Instructions");
        var retrievedAgent = await service.GetAgentAsync(createdAgent.Id);

        Assert.NotNull(retrievedAgent);
        Assert.Equal(createdAgent.Id, retrievedAgent.Id);
        Assert.Equal("Test Agent", retrievedAgent.Name);
    }

    [Fact]
    public async Task GetAgent_ShouldReturnNull_WhenNotExists()
    {
        var options = new DbContextOptionsBuilder<MiniBrainDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new MiniBrainDbContext(options);
        var service = new AgentService(context);

        var retrievedAgent = await service.GetAgentAsync(Guid.NewGuid());

        Assert.Null(retrievedAgent);
    }
}

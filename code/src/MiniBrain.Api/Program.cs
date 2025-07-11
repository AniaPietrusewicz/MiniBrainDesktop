using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;
using MiniBrain.Infrastructure.Data;
using MiniBrain.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ClaudeApiSettings>(builder.Configuration.GetSection("ClaudeApi"));
builder.Services.Configure<QdrantSettings>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<MemoryServiceSettings>(builder.Configuration.GetSection("MemoryService"));
builder.Services.Configure<MiniBrainSettings>(builder.Configuration.GetSection("MiniBrain"));

builder.Services.AddDbContext<MiniBrainDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IMiniBrainDbContext>(provider => provider.GetRequiredService<MiniBrainDbContext>());

builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>();
builder.Services.AddHttpClient<IWebBrowsingService, WebBrowsingService>();
builder.Services.AddHttpClient<IQdrantHealthService, QdrantHealthService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

builder.Services.AddScoped<ISemanticChunker, SemanticChunker>();
builder.Services.AddScoped<IEmbeddingService, CustomEmbeddingService>();
builder.Services.AddScoped<IQdrantClient>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<QdrantClientWrapper>>();
    var baseUrl = config.GetSection("Qdrant")["BaseUrl"] ?? "http://localhost:6333";
    return new QdrantClientWrapper(baseUrl, logger);
});
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddSingleton<IVectorSearchService, SimpleVectorSearchService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MiniBrainDbContext>();
    context.Database.EnsureCreated();
    
    var qdrantHealth = scope.ServiceProvider.GetRequiredService<IQdrantHealthService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    
    var qdrantEnabled = bool.Parse(config.GetSection("Qdrant")["Enabled"] ?? "true");
    
    if (!qdrantEnabled)
    {
        logger.LogWarning("🚫 Qdrant is disabled in configuration - vector search features will not be available");
    }
    else
    {
        logger.LogInformation("🔍 Checking Qdrant availability on startup...");
        
        var qdrantAvailable = await qdrantHealth.EnsureQdrantAvailableAsync();
        
        if (!qdrantAvailable)
        {
            logger.LogError("❌ CRITICAL: Qdrant is not available!");
            logger.LogError("💡 The application will start but memory/vector search features may not work properly.");
            logger.LogError("💡 Please ensure Docker is running and execute: docker start qdrant");
            logger.LogError("💡 Or disable Qdrant by setting Qdrant:Enabled = false in appsettings.json");
        }
        else
        {
            logger.LogInformation("✅ Qdrant is available and healthy");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

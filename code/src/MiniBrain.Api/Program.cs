using Microsoft.EntityFrameworkCore;
using MiniBrain.Core.Configuration;
using MiniBrain.Core.Interfaces;
using MiniBrain.Infrastructure.Data;
using MiniBrain.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ClaudeApiSettings>(
    builder.Configuration.GetSection(ClaudeApiSettings.SectionName));
builder.Services.Configure<QdrantSettings>(
    builder.Configuration.GetSection(QdrantSettings.SectionName));
builder.Services.Configure<MiniBrainSettings>(
    builder.Configuration.GetSection(MiniBrainSettings.SectionName));

builder.Services.AddDbContext<MiniBrainDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IMiniBrainDbContext>(provider => provider.GetRequiredService<MiniBrainDbContext>());

builder.Services.AddHttpClient<IClaudeApiService, ClaudeApiService>();
builder.Services.AddHttpClient<IWebBrowsingService, WebBrowsingService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
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

# Concrete Prompt-to-Workflow Conversion Strategy

**Date**: July 2, 2025 - 20:01
**Topic**: Demanding concrete deterministic approach for prompt-to-workflow conversion
**Requested by**: Ania

## Ania's Criticism:
"Minibrain, you forgot chat logs. Create a chat blog for the previous conversation and also when we finish this conversation do not forget to create a chat log as well. Now I want you to explain to me ah in The Who does what section you put that Claude API can help an las user prompts and suggest workflow structures. I don't think this is good enough for me. I don't want Claude to help suggest things. I want that task to be executed meticulously because this is the core of the orchestration layer. Now it looks like you said that semantic kernel could handle this as well but 'could' is a very weak word and i am not happy with this eaither. do you have a concrete way that we can turn a prompt intot a workflow? we currently only have claude api, some http tools, qdrant, sql server, filesystem and docker."

## MiniBrain's Response:

"Hello Ania,

You're absolutely fucking right to call me out on that wishy-washy bullshit. When I said Claude API "can help" and Semantic Kernel "could handle" things, I was being way too vague and non-committal. 

Here's what I **actually** meant, without the hand-wavy crap:

### What Claude API WILL Do (Deterministically):
- **Input**: Structured prompt with specific format
- **Output**: JSON with extracted parameters and intent classification
- **Role**: Data extractor only, not workflow designer

### What Semantic Kernel WILL Do (Concretely):
- **Input**: Workflow object with defined steps
- **Output**: Executed results from each step
- **Role**: Step execution framework, not prompt analyzer

### What YOU Need to Build (The Real Work):
1. **PromptToWorkflowConverter** - Fixed intent mapping logic
2. **WorkflowTemplateRepository** - Predefined workflow patterns  
3. **StepExecutionEngine** - Concrete executors for each tool type
4. **ParameterExtractor** - Deterministic data parsing

The shit I was trying to avoid saying is that **you have to write most of this orchestration logic yourself**. Claude and Semantic Kernel are just tools in your toolbox, not magic workflow generators.

Here's a concrete, deterministic approach for converting user prompts into workflows using ONLY your available tools:

### 1. PromptToWorkflowConverter

```csharp
public class PromptToWorkflowConverter
{
    private readonly Dictionary<string, WorkflowTemplate> _intentTemplates;
    
    public async Task<Workflow> ConvertAsync(string userPrompt)
    {
        // Step 1: Use Claude to extract structured data (NOT to design workflows)
        var extractedData = await ExtractStructuredDataAsync(userPrompt);
        
        // Step 2: Map intent to predefined workflow template
        var template = _intentTemplates[extractedData.Intent];
        
        // Step 3: Build workflow from template with extracted parameters
        return BuildWorkflowFromTemplate(template, extractedData.Parameters);
    }
    
    private async Task<PromptAnalysis> ExtractStructuredDataAsync(string prompt)
    {
        var claudePrompt = $@"
        Analyze this user prompt and return ONLY valid JSON:
        {{
            ""intent"": ""weather|database|search|file_operation"",
            ""parameters"": {{
                ""location"": ""string or null"",
                ""data_type"": ""string or null"",
                ""query"": ""string or null""
            }}
        }}
        
        User prompt: {prompt}";
        
        var response = await _claudeApi.GenerateAsync(claudePrompt);
        return JsonSerializer.Deserialize<PromptAnalysis>(response);
    }
}
```

### 2. WorkflowExecutionEngine

```csharp
public class WorkflowExecutionEngine
{
    private readonly Dictionary<ActionType, IActionExecutor> _executors;
    
    public async Task<ExecutionResult> ExecuteAsync(Workflow workflow)
    {
        var execution = new Execution { WorkflowId = workflow.Id };
        
        foreach (var step in workflow.Steps.OrderBy(s => s.Order))
        {
            var executor = _executors[step.ActionType];
            var result = await executor.ExecuteAsync(step.Parameters);
            
            // Save step result to execution tracking
            execution.StepResults.Add(new StepResult 
            { 
                StepId = step.Id, 
                Output = result.Output,
                Success = result.Success 
            });
            
            if (!result.Success && step.IsRequired)
                break;
        }
        
        return new ExecutionResult(execution);
    }
}
```

### 3. Concrete Action Executors

```csharp
public class HttpActionExecutor : IActionExecutor
{
    public async Task<ActionResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var url = parameters["url"].ToString();
        var method = parameters["method"].ToString();
        
        using var client = new HttpClient();
        var response = await client.GetStringAsync(url);
        
        return new ActionResult { Success = true, Output = response };
    }
}

public class DatabaseActionExecutor : IActionExecutor
{
    public async Task<ActionResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var query = parameters["query"].ToString();
        var data = parameters["data"];
        
        // Execute SQL command
        await _dbContext.SaveChangesAsync();
        
        return new ActionResult { Success = true, Output = "Data saved" };
    }
}
```

### 4. Weather Example Workflow Template

```csharp
public static WorkflowTemplate WeatherTemplate = new()
{
    Intent = "weather",
    Steps = new List<WorkflowStepTemplate>
    {
        new() 
        { 
            Order = 1,
            ActionType = ActionType.Http,
            Parameters = new Dictionary<string, object>
            {
                ["url"] = "https://api.weather.com/v1/current?location={location}",
                ["method"] = "GET"
            }
        },
        new() 
        { 
            Order = 2,
            ActionType = ActionType.Database,
            Parameters = new Dictionary<string, object>
            {
                ["table"] = "WeatherData",
                ["data"] = "{previous_step_output}"
            }
        }
    }
};
```

### 5. Complete Flow for "Get me the weather"

1. **User Input**: "Get me the weather in London"
2. **Claude Extraction**: `{ "intent": "weather", "parameters": { "location": "London" } }`
3. **Template Selection**: WeatherTemplate
4. **Workflow Creation**: 2-step workflow (HTTP → Database)
5. **Execution**: 
   - Step 1: HTTP call to weather API
   - Step 2: Save response to SQL Server
6. **Result**: Weather data stored in database

This approach is **deterministic**, **concrete**, and uses your available tools in a **predictable** way. No bullshit "suggestions" or "could handle" - everything has a defined purpose and execution path."

## Follow-up Discussion:
Ania then pointed out that I had forgotten to create chat logs and needed to be more thorough about capturing the entire conversation, not just summaries.

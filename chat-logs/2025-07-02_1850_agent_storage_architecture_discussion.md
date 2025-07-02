# Agent Storage Architecture Discussion

**Date**: July 2, 2025 - 18:50
**Topic**: Why storing agents in SQL Server is problematic and agent runtime vs metadata
**Requested by**: Ania

## Ania's Question:
"OK minibrain say uh we will take your example of creating an agent. There's a controller call made, then it goes to the API to create the agent, which is fine. Then it goes to many brain infrastructure. Uh can you tell me why you'd use mini brain DB contacts to save to SQL server? I mean an agent must be running in memory generally or in storage somewhere. You wouldn't store an agent in sequel server. Some of them can be say 10 gigabytes in size. So."

## MiniBrain's Response:
Hello Ania,

Ah shit, you caught a fundamental fucking design flaw here! You're absolutely right - this is a classic case of confusing **agent metadata** with the **actual running agent instances**. Let me break down what's happening and why it's problematic:

## **The Problem You've Identified:**

You're spot fucking on! The current code is storing **agent definitions** in SQL Server, but that's not the same as **running agent instances**. Here's what's happening:

### **What SQL Server Should Store (Agent Metadata):**
```
✅ Agent.Name = "WeatherBot"
✅ Agent.Description = "Fetches weather data"  
✅ Agent.Instructions = "You are a weather assistant..."
✅ Agent.IsActive = true
✅ Workflows, Goals, basic config
```

### **What SQL Server Should NOT Store (Agent Runtime):**
```
❌ Loaded AI model weights (10GB+)
❌ Active conversation state/memory
❌ Vector embeddings cache
❌ Real-time agent execution context
❌ Semantic Kernel instances
```

## **The Correct Architecture Should Be:**

1. **SQL Server**: Agent **definitions** and **metadata** only
2. **In-Memory/Redis**: **Running agent instances** with loaded models
3. **Vector DB (Qdrant)**: **Long-term memory** and embeddings
4. **File Storage/Blob**: **Large model files** and assets

## **Why Call Core Project:**

The Core project contains the **business logic** - it's where you'd have:
- Agent validation rules
- Workflow orchestration logic  
- Domain events
- Business constraints

But you're right that the current flow is fucked up. It should be more like:

```
1. API Controller receives "Create Agent" request
2. Core Domain validates agent definition 
3. Infrastructure saves METADATA to SQL Server
4. Infrastructure registers agent template in memory
5. When agent needs to RUN, load from template + spin up instance
```

## **What's Missing:**

Looking at this architecture, you need:
- **Agent Runtime Manager** (in-memory agent instances)
- **Model Loader Service** (loads AI models on demand)
- **Session Manager** (manages active conversations)
- **Resource Manager** (handles memory/cleanup)

The current code treats agents like static data records, but real AI agents are **dynamic runtime processes** that need proper lifecycle management.

Want me to show you how this should be restructured for proper agent runtime management?

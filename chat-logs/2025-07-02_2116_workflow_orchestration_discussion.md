# Workflow Orchestration Architecture Discussion

**Date**: July 2, 2025 - 19:57
**Topic**: How to convert prompts into workflows and execute them
**Requested by**: Ania

## Ania's Question:
"Minibrain who umm, converts the prompt into a workflow and then who takes the workflow and converts it into steps? Is that by done by the Claude API call or would that be done by the semantic kernel? Or do I have to write that myself?"

## MiniBrain's Response:
Explained that you need to build orchestration services yourself - neither Claude nor Semantic Kernel will automatically convert prompts to workflows. Suggested:

1. **WorkflowGenerationService** - Uses Claude to analyze prompts and suggest workflow steps
2. **WorkflowExecutionService** - Executes workflows step by step
3. **ActionExecutors** - Handle specific action types (HTTP, DB, etc.)

Provided code examples showing how Claude could "help suggest" workflow structures and how Semantic Kernel "could handle" orchestration.

## Key Point:
The workflow models are data structures but need orchestration layer to bring them to life.

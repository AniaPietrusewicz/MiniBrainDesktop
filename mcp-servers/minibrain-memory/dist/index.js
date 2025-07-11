#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema, } from '@modelcontextprotocol/sdk/types.js';
import axios from 'axios';
const API_BASE_URL = process.env.MINIBRAIN_API_URL || 'http://localhost:5000';
const server = new Server({
    name: 'minibrain-memory',
    version: '1.0.0',
});
server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
        tools: [
            {
                name: 'intelligent_memory_search',
                description: 'Search MiniBrain memories using AI-selected optimal strategy. Choose the best strategy: semantic (general questions), context-aware (conversation-specific), hybrid (complex queries), temporal (time-based), recent (latest memories), similar (content similarity)',
                inputSchema: {
                    type: 'object',
                    properties: {
                        query: {
                            type: 'string',
                            description: 'The search query or question about memories'
                        },
                        preferredStrategy: {
                            type: 'string',
                            enum: ['semantic', 'context-aware', 'hybrid', 'temporal', 'recent', 'similar'],
                            description: 'Search strategy: semantic=general search, context-aware=current conversation, hybrid=complex queries, temporal=time range, recent=latest memories, similar=content similarity',
                            default: 'semantic'
                        },
                        searchContext: {
                            type: 'string',
                            description: 'Your reasoning for choosing this strategy'
                        },
                        conversationId: {
                            type: 'string',
                            description: 'Current conversation ID (for context-aware searches)'
                        },
                        sessionId: {
                            type: 'string',
                            description: 'Current session ID (for context-aware searches)'
                        },
                        hours: {
                            type: 'number',
                            description: 'Hours back to search (for recent searches)',
                            default: 24
                        },
                        threshold: {
                            type: 'number',
                            description: 'Similarity threshold 0.0-1.0 (for similar searches)',
                            default: 0.8
                        },
                        limit: {
                            type: 'number',
                            description: 'Maximum memories to return',
                            default: 10
                        }
                    },
                    required: ['query', 'preferredStrategy', 'searchContext']
                }
            }
        ]
    };
});
server.setRequestHandler(CallToolRequestSchema, async (request) => {
    if (request.params.name !== 'intelligent_memory_search') {
        throw new Error(`Unknown tool: ${request.params.name}`);
    }
    try {
        const args = request.params.arguments;
        // Build the request based on strategy
        let searchRequest = {
            query: args.query,
            preferredStrategy: args.preferredStrategy || 'semantic',
            searchContext: args.searchContext || 'No context provided',
            limit: args.limit || 10,
            threshold: args.threshold || 0.8
        };
        // Add strategy-specific parameters
        if (args.conversationId)
            searchRequest.conversationId = args.conversationId;
        if (args.sessionId)
            searchRequest.sessionId = args.sessionId;
        if (args.hours) {
            searchRequest.timeSpan = `${args.hours}:00:00`;
        }
        const response = await axios.post(`${API_BASE_URL}/api/memory/search/intelligent`, searchRequest);
        const result = response.data;
        let resultText = `## Memory Search Results\n\n`;
        resultText += `**Strategy Used:** ${result.strategy_used}\n`;
        resultText += `**Search Context:** ${result.context}\n`;
        resultText += `**Results Found:** ${result.memories?.length || 0}\n\n`;
        if (result.memories && result.memories.length > 0) {
            result.memories.forEach((memory, index) => {
                resultText += `### ${index + 1}. [${memory.role}] ${memory.timestamp}\n`;
                resultText += `**Content:** ${memory.content}\n`;
                resultText += `**Session:** ${memory.sessionId}\n`;
                resultText += `**Conversation:** ${memory.conversationId}\n`;
                if (memory.importanceScore) {
                    resultText += `**Importance:** ${memory.importanceScore}\n`;
                }
                resultText += `\n---\n\n`;
            });
        }
        else {
            resultText += "No memories found matching the search criteria.";
        }
        return {
            content: [
                {
                    type: 'text',
                    text: resultText
                }
            ],
            isError: false
        };
    }
    catch (error) {
        const errorMessage = axios.isAxiosError(error)
            ? `API Error: ${error.response?.status} - ${error.response?.data?.error || error.message}`
            : `Error: ${error instanceof Error ? error.message : String(error)}`;
        return {
            content: [
                {
                    type: 'text',
                    text: `Failed to search memories: ${errorMessage}`
                }
            ],
            isError: true
        };
    }
});
async function main() {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error('MiniBrain Memory MCP server running on stdio');
}
main().catch(console.error);
//# sourceMappingURL=index.js.map
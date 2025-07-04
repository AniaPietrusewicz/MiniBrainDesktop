# Claude API Debugging Session - July 3, 2025

## Issue
Getting authentication error when calling Claude API despite having valid API key.

## Working curl command
```bash
curl https://api.anthropic.com/v1/messages \
    --header "x-api-key: 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" \
    --header "anthropic-version: 2023-06-01" \
    --header "content-type: application/json" \
    --data \
'{
    "model": "claude-sonnet-4-20250514",
    "max_tokens": 20000,
    "temperature": 0.5,
    "messages": [
        {"role": "user", "content": "Hello, world"}
    ]
}'
```

## Changes Made
1. Fixed JSON serialization options
2. Hardcoded model name and max tokens to match curl
3. Set anthropic-version to "2023-06-01" 
4. Explicitly set content-type header

## Current Status
Still getting 401 authentication error. Need to investigate further.

## Next Steps
- Check if API key is being truncated
- Verify exact request format
- Test direct curl call from same environment

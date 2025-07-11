# Chat Log: gRPC Connection Error Diagnosis and Fix

**Date:** July 8, 2025  
**Time:** 17:00  
**Issue:** Desktop app gRPC connection error when sending messages to Claude API

## User Request
```
i try to send a message to claude api through the ui and i am getting this error: `Status(StatusCode="Unavailable", Detail="Error connecting to subchannel.", DebugException="System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it.")` diagnose and fix please. api already running. do not use curl requests, only api calls
```

## MiniBrain Response

Hello Ania, I need to diagnose this fucking gRPC connection error. Let me check the API setup and desktop app configuration to see what's causing the connection refusal.

### Investigation Process

1. **Checked API Configuration**: Verified appsettings.json and Program.cs - no gRPC services configured in API
2. **Searched for gRPC References**: Found gRPC only used for Qdrant client connections (QdrantClientWrapper)
3. **Analyzed Desktop App**: Confirmed it uses HTTP calls to API on port 5089, not gRPC
4. **Checked API Status**: API confirmed running on port 5089 (TCP 127.0.0.1:5089)
5. **Investigated Qdrant Status**: **ROOT CAUSE FOUND** - Qdrant container was not running

### Root Cause Analysis

The gRPC error was NOT from the desktop app to API communication (which uses HTTP). The error was occurring when the API tried to connect to Qdrant's gRPC service (port 6334) for memory/vector search functionality.

**Error Flow:**
1. Desktop app sends HTTP request to API
2. API processes request and tries to access Qdrant for memory services
3. QdrantClientWrapper attempts gRPC connection to localhost:6334
4. Connection fails because Qdrant container was stopped
5. gRPC error bubbles up through the API response

### Fix Applied

```bash
# Started the existing Qdrant container
docker start qdrant

# Verified container is running with proper port mapping
docker ps
# Result: qdrant/qdrant running on 0.0.0.0:6333-6334->6333-6334/tcp

# Confirmed both HTTP and gRPC ports are now accessible
netstat -ano | findstr :633
# Result: Both 6333 (HTTP) and 6334 (gRPC) listening
# Established connection visible: API already connected to Qdrant gRPC
```

### Technical Details

- **Qdrant Ports**: 6333 (HTTP API), 6334 (gRPC)
- **API Architecture**: Uses QdrantClientWrapper for vector search
- **Connection Pattern**: API→Qdrant gRPC for semantic search/memory
- **Fix**: Simply started the stopped Qdrant Docker container

### Status
✅ **RESOLVED**: Qdrant container now running, gRPC connections established, desktop app should work normally.

The fucking issue was just that Qdrant wasn't running - classic infrastructure problem disguised as a complex application error!

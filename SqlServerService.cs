using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniBrain.Orchestration.Interfaces;
using MiniBrain.Orchestration.Models;

namespace MiniBrain.Orchestration.Services;

public class SqlServerService : ISqlServerService
{
    private readonly IConfiguration _configuration;
    private readonly IAuthService _authService;
    private readonly ILogger<SqlServerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SqlServerService(IConfiguration configuration, IAuthService authService, ILogger<SqlServerService> logger)
    {
        _configuration = configuration;
        _authService = authService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var action = GetStringProperty(input, "action") ?? "query";
            var query = GetStringProperty(input, "query");
            var database = GetStringProperty(input, "database") ?? "MiniBrainDb_Dev";
            var connectionName = GetStringProperty(input, "connection") ?? "DefaultConnection";
            
            if (string.IsNullOrEmpty(query))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = "SQL query is required",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            var connectionString = await GetConnectionStringAsync(connectionName, database, cancellationToken);
            if (string.IsNullOrEmpty(connectionString))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"Connection string '{connectionName}' not found in configuration",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            switch (action.ToLowerInvariant())
            {
                case "query":
                case "select":
                    return await ExecuteQuery(connectionString, query, stopwatch, cancellationToken);
                
                case "execute":
                case "command":
                    return await ExecuteCommand(connectionString, query, stopwatch, cancellationToken);
                
                case "scalar":
                    return await ExecuteScalar(connectionString, query, stopwatch, cancellationToken);
                
                case "test":
                case "ping":
                    return await TestConnection(connectionString, stopwatch, cancellationToken);
                
                default:
                    return new ToolResult
                    {
                        Success = false,
                        Output = $"Unknown action: {action}. Supported actions: query, execute, scalar, test",
                        ExecutionTime = stopwatch.Elapsed
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL Server operation");
            return new ToolResult
            {
                Success = false,
                Output = $"Error: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> ExecuteQuery(string connectionString, string query, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            var results = new List<Dictionary<string, object>>();
            var columnNames = new List<string>();
            
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }
            
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnNames[i]] = value;
                }
                results.Add(row);
            }

            var result = new
            {
                Action = "Query",
                RowCount = results.Count,
                Columns = columnNames,
                Data = results,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL execution error");
            return new ToolResult
            {
                Success = false,
                Output = $"SQL Error: {ex.Message} (Error Number: {ex.Number})",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> ExecuteCommand(string connectionString, string command, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var sqlCommand = new SqlCommand(command, connection);
            sqlCommand.CommandTimeout = 30;
            
            var rowsAffected = await sqlCommand.ExecuteNonQueryAsync(cancellationToken);

            var result = new
            {
                Action = "Command",
                RowsAffected = rowsAffected,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL command execution error");
            return new ToolResult
            {
                Success = false,
                Output = $"SQL Error: {ex.Message} (Error Number: {ex.Number})",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> ExecuteScalar(string connectionString, string query, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30;
            
            var result = await command.ExecuteScalarAsync(cancellationToken);

            var response = new
            {
                Action = "Scalar",
                Value = result,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(response, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL scalar execution error");
            return new ToolResult
            {
                Success = false,
                Output = $"SQL Error: {ex.Message} (Error Number: {ex.Number})",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> TestConnection(string connectionString, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var serverVersion = connection.ServerVersion;
            var database = connection.Database;

            var result = new
            {
                Action = "Test",
                Status = "Connected",
                ServerVersion = serverVersion,
                Database = database,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL connection test failed");
            return new ToolResult
            {
                Success = false,
                Output = $"Connection Failed: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<string?> GetConnectionStringAsync(string connectionName, string database, CancellationToken cancellationToken)
    {
        try
        {
            // Try to get connection string from vault first
            var vaultPath = $"database/{database.ToLowerInvariant()}";
            var vaultResult = await _authService.ExecuteAsync(JsonSerializer.SerializeToElement(new
            {
                action = "get",
                path = vaultPath,
                key = "connection_string"
            }), cancellationToken);

            if (vaultResult.Success && !string.IsNullOrEmpty(vaultResult.Output))
            {
                // Parse the vault response to extract the connection string
                var vaultResponse = JsonSerializer.Deserialize<JsonElement>(vaultResult.Output);
                if (vaultResponse.TryGetProperty("Value", out var valueElement) && 
                    valueElement.ValueKind == JsonValueKind.String)
                {
                    return valueElement.GetString();
                }
            }

            _logger.LogWarning("Could not retrieve connection string from vault for database '{Database}', falling back to configuration", database);
            
            // Fallback to configuration if vault fails
            var connectionString = _configuration.GetSection("ConnectionStrings")[connectionName];
            
            if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(database))
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = database
                };
                return builder.ConnectionString;
            }
            
            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connection string from vault, falling back to configuration");
            
            // Fallback to configuration on any error
            var connectionString = _configuration.GetSection("ConnectionStrings")[connectionName];
            
            if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(database))
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = database
                };
                return builder.ConnectionString;
            }
            
            return connectionString;
        }
    }

    private string? GetConnectionString(string connectionName, string database)
    {
        var connectionString = _configuration.GetSection("ConnectionStrings")[connectionName];
        
        if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(database))
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = database
            };
            return builder.ConnectionString;
        }
        
        return connectionString;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}

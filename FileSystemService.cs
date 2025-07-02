using System.Text.Json;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MiniBrain.Orchestration.Interfaces;
using MiniBrain.Orchestration.Models;

namespace MiniBrain.Orchestration.Services;

public class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemService(ILogger<FileSystemService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemService>.Instance;
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
            var action = GetStringProperty(input, "action") ?? "read";
            var path = GetStringProperty(input, "path");
            
            if (string.IsNullOrEmpty(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = "File path is required",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            path = Path.GetFullPath(path);

            switch (action.ToLowerInvariant())
            {
                case "read":
                case "get":
                    return await ReadFile(path, stopwatch, cancellationToken);
                
                case "write":
                case "create":
                    var content = GetStringProperty(input, "content") ?? "";
                    return await WriteFile(path, content, stopwatch, cancellationToken);
                
                case "append":
                    var appendContent = GetStringProperty(input, "content") ?? "";
                    return await AppendFile(path, appendContent, stopwatch, cancellationToken);
                
                case "delete":
                case "remove":
                    return await DeleteFile(path, stopwatch, cancellationToken);
                
                case "exists":
                case "check":
                    return await CheckExists(path, stopwatch, cancellationToken);
                
                case "info":
                case "stat":
                    return await GetFileInfo(path, stopwatch, cancellationToken);
                
                case "list":
                case "dir":
                    return await ListDirectory(path, stopwatch, cancellationToken);
                
                case "mkdir":
                case "createdir":
                    return await CreateDirectory(path, stopwatch, cancellationToken);
                
                case "copy":
                    var destination = GetStringProperty(input, "destination");
                    if (string.IsNullOrEmpty(destination))
                    {
                        return new ToolResult
                        {
                            Success = false,
                            Output = "Destination path is required for copy operation",
                            ExecutionTime = stopwatch.Elapsed
                        };
                    }
                    return await CopyFile(path, destination, stopwatch, cancellationToken);
                
                case "move":
                case "rename":
                    var newPath = GetStringProperty(input, "destination") ?? GetStringProperty(input, "newPath");
                    if (string.IsNullOrEmpty(newPath))
                    {
                        return new ToolResult
                        {
                            Success = false,
                            Output = "New path is required for move operation",
                            ExecutionTime = stopwatch.Elapsed
                        };
                    }
                    return await MoveFile(path, newPath, stopwatch, cancellationToken);
                
                case "hash":
                case "checksum":
                    return await GetFileHash(path, stopwatch, cancellationToken);
                
                default:
                    return new ToolResult
                    {
                        Success = false,
                        Output = $"Unknown action: {action}. Supported actions: read, write, append, delete, exists, info, list, mkdir, copy, move, hash",
                        ExecutionTime = stopwatch.Elapsed
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in file system operation");
            return new ToolResult
            {
                Success = false,
                Output = $"Error: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> ReadFile(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"File not found: {path}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var fileInfo = new FileInfo(path);

            var result = new
            {
                Action = "Read",
                Path = path,
                Content = content,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to read file: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> WriteFile(string path, string content, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, content, cancellationToken);
            var fileInfo = new FileInfo(path);

            var result = new
            {
                Action = "Write",
                Path = path,
                Size = fileInfo.Length,
                Created = fileInfo.CreationTime,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to write file: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> AppendFile(string path, string content, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            await File.AppendAllTextAsync(path, content, cancellationToken);
            var fileInfo = new FileInfo(path);

            var result = new
            {
                Action = "Append",
                Path = path,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to append to file: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> DeleteFile(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"Path not found: {path}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            var result = new
            {
                Action = "Delete",
                Path = path,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to delete: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> CheckExists(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        var fileExists = File.Exists(path);
        var dirExists = Directory.Exists(path);

        var result = new
        {
            Action = "Exists",
            Path = path,
            FileExists = fileExists,
            DirectoryExists = dirExists,
            Exists = fileExists || dirExists,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
        };

        return new ToolResult
        {
            Success = true,
            Output = JsonSerializer.Serialize(result, _jsonOptions),
            ExecutionTime = stopwatch.Elapsed
        };
    }

    private async Task<ToolResult> GetFileInfo(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        try
        {
            object result;

            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                result = new
                {
                    Action = "Info",
                    Path = path,
                    Type = "File",
                    Size = fileInfo.Length,
                    Created = fileInfo.CreationTime,
                    LastModified = fileInfo.LastWriteTime,
                    LastAccessed = fileInfo.LastAccessTime,
                    IsReadOnly = fileInfo.IsReadOnly,
                    Extension = fileInfo.Extension,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            else if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                result = new
                {
                    Action = "Info",
                    Path = path,
                    Type = "Directory",
                    Created = dirInfo.CreationTime,
                    LastModified = dirInfo.LastWriteTime,
                    LastAccessed = dirInfo.LastAccessTime,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            else
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"Path not found: {path}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to get file info: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> ListDirectory(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        try
        {
            if (!Directory.Exists(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"Directory not found: {path}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            var files = Directory.GetFiles(path).Select(f => new
            {
                Name = Path.GetFileName(f),
                Path = f,
                Type = "File",
                Size = new FileInfo(f).Length,
                LastModified = new FileInfo(f).LastWriteTime
            }).ToList();

            var directories = Directory.GetDirectories(path).Select(d => new
            {
                Name = Path.GetFileName(d),
                Path = d,
                Type = "Directory",
                Size = (long?)null,
                LastModified = new DirectoryInfo(d).LastWriteTime
            }).ToList();

            var items = files.Cast<object>().Concat(directories.Cast<object>()).ToList();

            var result = new
            {
                Action = "List",
                Path = path,
                ItemCount = items.Count,
                FileCount = files.Count,
                DirectoryCount = directories.Count,
                Items = items,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to list directory: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> CreateDirectory(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var result = new
            {
                Action = "CreateDirectory",
                Path = path,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to create directory: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> CopyFile(string source, string destination, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        try
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(source, destination, true);

            var result = new
            {
                Action = "Copy",
                Source = source,
                Destination = destination,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to copy file: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> MoveFile(string source, string destination, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        
        try
        {
            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(source, destination);

            var result = new
            {
                Action = "Move",
                Source = source,
                Destination = destination,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to move file: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<ToolResult> GetFileHash(string path, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = $"File not found: {path}",
                    ExecutionTime = stopwatch.Elapsed
                };
            }

            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var result = new
            {
                Action = "Hash",
                Path = path,
                Algorithm = "SHA256",
                Hash = hash,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };

            return new ToolResult
            {
                Success = true,
                Output = JsonSerializer.Serialize(result, _jsonOptions),
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = $"Failed to compute file hash: {ex.Message}",
                ExecutionTime = stopwatch.Elapsed
            };
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}

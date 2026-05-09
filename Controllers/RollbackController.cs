using Microsoft.AspNetCore.Mvc;
using MSLX.SDK.Models;
using MSLX.SDK;
using System.IO.Compression;
using System.Text.Json;
using System.Reflection;

namespace MSLX.Plugin.Demo.Controllers;

[ApiController]
[Route("api/plugins/mslx-plugin-demo/rollback")]
public class RollbackController : ControllerBase
{
    private string? GetPropertyValue(object obj, params string[] propertyNames)
    {
        if (obj == null) return null;
        
        var type = obj.GetType();
        SDK.MSLX.Logger.Info($"GetPropertyValue - Type: {type.FullName}, Looking for: {string.Join(", ", propertyNames)}");
        
        foreach (var name in propertyNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                SDK.MSLX.Logger.Info($"  Found property '{name}' with value: {value ?? "null"}");
                if (value != null) return value.ToString();
            }
            else
            {
                SDK.MSLX.Logger.Info($"  Property '{name}' not found");
            }
        }
        return null;
    }

    private bool? GetBoolPropertyValue(object obj, params string[] propertyNames)
    {
        if (obj == null) return null;
        
        foreach (var name in propertyNames)
        {
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                var value = prop.GetValue(obj);
                if (value != null) return (bool)value;
            }
        }
        return null;
    }

    private void LogObjectProperties(object obj, string objName)
    {
        if (obj == null)
        {
            SDK.MSLX.Logger.Info($"{objName} is null");
            return;
        }
        
        var type = obj.GetType();
        SDK.MSLX.Logger.Info($"{objName} type: {type.FullName}");
        
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(obj);
                SDK.MSLX.Logger.Info($"  {prop.Name} ({prop.PropertyType.Name}): {value ?? "null"}");
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Info($"  {prop.Name}: Error getting value - {ex.Message}");
            }
        }
    }

    [HttpGet("backups")]
    public IActionResult GetBackups()
    {
        try
        {
            SDK.MSLX.Logger.Info("=== GetBackups called ===");
            
            var servers = SDK.MSLX.Config.Servers.GetServerList();
            if (servers == null)
            {
                SDK.MSLX.Logger.Error("GetServerList() returned null");
                return Ok(new ApiResponse<List<BackupInfo>>
                {
                    Code = 200,
                    Message = "无法获取服务器列表",
                    Data = new List<BackupInfo>()
                });
            }
            
            SDK.MSLX.Logger.Info($"GetServerList() returned {servers.Count} servers");
            
            if (!servers.Any())
            {
                SDK.MSLX.Logger.Info("No servers found");
                return Ok(new ApiResponse<List<BackupInfo>>
                {
                    Code = 200,
                    Message = "暂无服务器实例",
                    Data = new List<BackupInfo>()
                });
            }

            var firstServer = servers.First();
            LogObjectProperties(firstServer, "FirstServer");
            
            var serverPath = GetPropertyValue(firstServer, "Base", "ServerPath", "Path", "InstancePath", "ServerDir", "Dir", "WorkingDirectory", "ServerFolder", "BasePath", "RootPath", "HomePath", "ServerRoot", "InstallPath", "ExecutablePath");
            var backupPath = GetPropertyValue(firstServer, "BackupPath", "BackupDir", "BackupFolder");
            
            SDK.MSLX.Logger.Info($"Detected serverPath: {serverPath ?? "null"}, backupPath: {backupPath ?? "null"}");
            
            string? backupDir = null;
            
            if (!string.IsNullOrEmpty(backupPath))
            {
                SDK.MSLX.Logger.Info($"Using BackupPath from server config: {backupPath}");
                if (Directory.Exists(backupPath))
                {
                    backupDir = backupPath;
                }
                else
                {
                    SDK.MSLX.Logger.Info($"BackupPath {backupPath} does not exist");
                }
            }
            
            if (backupDir == null && !string.IsNullOrEmpty(serverPath))
            {
                var possibleBackupDirs = new List<string>
                {
                    System.IO.Path.Combine(serverPath, "backups"),
                    System.IO.Path.Combine(serverPath, "backup"),
                    serverPath
                };
                
                foreach (var dir in possibleBackupDirs)
                {
                    SDK.MSLX.Logger.Info($"Checking backup directory: {dir} (exists: {Directory.Exists(dir)})");
                    if (Directory.Exists(dir))
                    {
                        backupDir = dir;
                        break;
                    }
                }
            }
            
            if (backupDir == null)
            {
                SDK.MSLX.Logger.Info("No backup directory found");
                return Ok(new ApiResponse<List<BackupInfo>>
                {
                    Code = 200,
                    Message = "未找到备份目录，请确保备份已创建",
                    Data = new List<BackupInfo>()
                });
            }

            SDK.MSLX.Logger.Info($"Using backup directory: {backupDir}");
            
            var backupFiles = new List<BackupInfo>();
            try
            {
                var zipFiles = Directory.GetFiles(backupDir, "*.zip", SearchOption.TopDirectoryOnly);
                SDK.MSLX.Logger.Info($"Found {zipFiles.Length} zip files");
                
                backupFiles = zipFiles
                    .Select(file => new BackupInfo
                    {
                        FileName = System.IO.Path.GetFileName(file),
                        FilePath = file,
                        CreateTime = System.IO.File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm:ss"),
                        Size = new FileInfo(file).Length
                    })
                    .OrderByDescending(b => b.CreateTime)
                    .ToList();
                
                SDK.MSLX.Logger.Info($"Returning {backupFiles.Count} backup files");
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"Error reading backup files: {ex}");
            }

            return Ok(new ApiResponse<List<BackupInfo>>
            {
                Code = 200,
                Message = "success",
                Data = backupFiles
            });
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"GetBackups failed: {ex}");
            return Ok(new ApiResponse<List<BackupInfo>>
            {
                Code = 500,
                Message = $"获取备份列表失败: {ex.Message}",
                Data = new List<BackupInfo>()
            });
        }
    }

    [HttpGet("server-info")]
    public IActionResult GetServerInfo()
    {
        try
        {
            SDK.MSLX.Logger.Info("=== GetServerInfo called ===");
            
            var servers = SDK.MSLX.Config.Servers.GetServerList();
            if (servers == null)
            {
                SDK.MSLX.Logger.Error("GetServerList() returned null");
                return Ok(new ApiResponse<ServerInfo>
                {
                    Code = 200,
                    Message = "无法获取服务器列表",
                    Data = null
                });
            }
            
            SDK.MSLX.Logger.Info($"GetServerList() returned {servers.Count} servers");
            
            if (!servers.Any())
            {
                SDK.MSLX.Logger.Info("No servers found");
                return Ok(new ApiResponse<ServerInfo>
                {
                    Code = 200,
                    Message = "暂无服务器实例",
                    Data = null
                });
            }

            var server = servers.First();
            LogObjectProperties(server, "Server");
            
            var serverPath = GetPropertyValue(server, "Base", "ServerPath", "Path", "InstancePath", "ServerDir", "Dir", "WorkingDirectory", "ServerFolder", "BasePath", "RootPath", "HomePath", "ServerRoot", "InstallPath", "ExecutablePath");
            var serverId = GetPropertyValue(server, "Id", "ServerId", "InstanceId", "Guid", "ServerGuid");
            var serverName = GetPropertyValue(server, "Name", "ServerName", "InstanceName", "DisplayName", "Title");
            var backupPath = GetPropertyValue(server, "BackupPath", "BackupDir", "BackupFolder");
            
            bool isRunning = false;
            int playerCount = 0;
            try
            {
                var serverType = server.GetType();
                var statusProp = serverType.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var isRunningProp = serverType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var onlinePlayerCountProp = serverType.GetProperty("OnlinePlayerCount", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var playerCountProp = serverType.GetProperty("PlayerCount", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                
                if (statusProp != null)
                {
                    var statusValue = statusProp.GetValue(server);
                    SDK.MSLX.Logger.Info($"Status property value: {statusValue} (type: {statusValue?.GetType().Name})");
                    if (statusValue != null)
                    {
                        string statusStr = statusValue.ToString()?.ToLower() ?? string.Empty;
                        isRunning = statusStr == "running" || statusStr == "started" || statusStr == "active";
                    }
                }
                
                if (isRunningProp != null)
                {
                    var isRunningValue = isRunningProp.GetValue(server);
                    if (isRunningValue is bool boolValue)
                    {
                        isRunning = boolValue;
                        SDK.MSLX.Logger.Info($"Got IsRunning from property: {isRunning}");
                    }
                }
                
                if (onlinePlayerCountProp != null)
                {
                    var countValue = onlinePlayerCountProp.GetValue(server);
                    if (countValue is int intValue)
                    {
                        playerCount = intValue;
                        SDK.MSLX.Logger.Info($"Got OnlinePlayerCount from property: {playerCount}");
                    }
                }
                else if (playerCountProp != null)
                {
                    var countValue = playerCountProp.GetValue(server);
                    if (countValue is int intValue)
                    {
                        playerCount = intValue;
                        SDK.MSLX.Logger.Info($"Got PlayerCount from property: {playerCount}");
                    }
                }
            }
            catch (Exception statusEx)
            {
                SDK.MSLX.Logger.Info($"Failed to get server status: {statusEx.Message}");
            }
            
            SDK.MSLX.Logger.Info($"Extracted - serverId: {serverId}, serverName: {serverName}, serverPath: {serverPath}, backupPath: {backupPath}, isRunning: {isRunning}");
            
            string? worldPath = null;
            if (!string.IsNullOrEmpty(serverPath))
            {
                var possibleWorldPaths = new List<string>
                {
                    System.IO.Path.Combine(serverPath, "world"),
                    System.IO.Path.Combine(serverPath, "save", "world"),
                    System.IO.Path.Combine(serverPath, "saves", "world"),
                    System.IO.Path.Combine(serverPath, "server", "world"),
                    System.IO.Path.Combine(serverPath, "World")
                };
                
                foreach (var path in possibleWorldPaths)
                {
                    SDK.MSLX.Logger.Info($"Checking world path: {path} (exists: {Directory.Exists(path)})");
                    if (Directory.Exists(path))
                    {
                        worldPath = path;
                        break;
                    }
                }
            }
            
            SDK.MSLX.Logger.Info($"Using worldPath: {worldPath ?? "not found"}");
            
            var result = new ServerInfo
            {
                ServerId = serverId ?? "",
                ServerName = serverName ?? "未命名服务器",
                ServerPath = serverPath ?? "",
                WorldPath = worldPath ?? "",
                BackupPath = backupPath ?? "",
                IsRunning = isRunning,
                PlayerCount = playerCount
            };
            
            SDK.MSLX.Logger.Info($"Returning server info: {JsonSerializer.Serialize(result)}");
            
            return Ok(new ApiResponse<ServerInfo>
            {
                Code = 200,
                Message = "success",
                Data = result
            });
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"GetServerInfo failed: {ex}");
            return Ok(new ApiResponse<ServerInfo>
            {
                Code = 500,
                Message = $"获取服务器信息失败: {ex.Message}",
                Data = null
            });
        }
    }

    [HttpPost("execute")]
    public IActionResult ExecuteRollback([FromBody] RollbackRequest request)
    {
        try
        {
            SDK.MSLX.Logger.Info("=== ExecuteRollback called ===");
            SDK.MSLX.Logger.Info($"Request: {JsonSerializer.Serialize(request)}");
            
            if (string.IsNullOrEmpty(request.BackupPath) || !System.IO.File.Exists(request.BackupPath))
            {
                SDK.MSLX.Logger.Error($"Backup file not found: {request.BackupPath}");
                return Ok(new ApiResponse<string>
                {
                    Code = 400,
                    Message = "备份文件不存在",
                    Data = null
                });
            }

            if (string.IsNullOrEmpty(request.WorldPath))
            {
                SDK.MSLX.Logger.Error("World path is empty");
                return Ok(new ApiResponse<string>
                {
                    Code = 400,
                    Message = "存档路径不能为空",
                    Data = null
                });
            }

            var logEntry = new RollbackLog
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                BackupFileName = System.IO.Path.GetFileName(request.BackupPath),
                TargetPath = request.WorldPath,
                Status = "started"
            };

            try
            {
                if (Directory.Exists(request.WorldPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupDir = $"{request.WorldPath}_backup_{timestamp}";
                    SDK.MSLX.Logger.Info($"Backing up existing world to: {backupDir}");
                    Directory.Move(request.WorldPath, backupDir);
                }

                SDK.MSLX.Logger.Info($"Extracting backup: {request.BackupPath} -> {request.WorldPath}");
                Directory.CreateDirectory(request.WorldPath);
                ZipFile.ExtractToDirectory(request.BackupPath, request.WorldPath);

                logEntry.Status = "success";
                SDK.MSLX.Logger.Info("Rollback completed successfully");
                SaveLog(logEntry);

                return Ok(new ApiResponse<string>
                {
                    Code = 200,
                    Message = "回档成功",
                    Data = "回档操作已完成，请手动重启服务器"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                SDK.MSLX.Logger.Error($"Permission denied: {ex}");
                logEntry.Status = "failed";
                SaveLog(logEntry);
                return Ok(new ApiResponse<string>
                {
                    Code = 403,
                    Message = $"权限不足: {ex.Message}",
                    Data = null
                });
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"Rollback failed: {ex}");
                logEntry.Status = "failed";
                SaveLog(logEntry);
                return Ok(new ApiResponse<string>
                {
                    Code = 500,
                    Message = $"回档失败: {ex.Message}",
                    Data = null
                });
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"ExecuteRollback failed: {ex}");
            return Ok(new ApiResponse<string>
            {
                Code = 500,
                Message = $"回档失败: {ex.Message}",
                Data = null
            });
        }
    }

    [HttpGet("logs")]
    public IActionResult GetRollbackLogs()
    {
        try
        {
            var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "rollback_logs");
            if (!Directory.Exists(logDir))
            {
                return Ok(new ApiResponse<List<RollbackLog>>
                {
                    Code = 200,
                    Message = "暂无回档日志",
                    Data = new List<RollbackLog>()
                });
            }

            var logFiles = Directory.GetFiles(logDir, "*.json")
                .OrderByDescending(f => f)
                .Take(20)
                .Select(file => 
                {
                    try 
                    { 
                        var content = System.IO.File.ReadAllText(file);
                        return JsonSerializer.Deserialize<RollbackLog>(content); 
                    }
                    catch { return null; }
                })
                .Where(l => l != null)
                .Select(l => l!)
                .ToList();

            return Ok(new ApiResponse<List<RollbackLog>>
            {
                Code = 200,
                Message = "success",
                Data = logFiles
            });
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"GetRollbackLogs failed: {ex}");
            return Ok(new ApiResponse<List<RollbackLog>>
            {
                Code = 500,
                Message = $"获取回档日志失败: {ex.Message}",
                Data = new List<RollbackLog>()
            });
        }
    }

    private void SaveLog(RollbackLog log)
    {
        try
        {
            var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "rollback_logs");
            Directory.CreateDirectory(logDir);
            
            var logFile = System.IO.Path.Combine(logDir, $"rollback_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            System.IO.File.WriteAllText(logFile, JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"SaveLog failed: {ex}");
        }
    }
}

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
}

public class BackupInfo
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string CreateTime { get; set; } = "";
    public long Size { get; set; }
}

public class ServerInfo
{
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerPath { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public bool IsRunning { get; set; }
    public int PlayerCount { get; set; }
}

public class RollbackRequest
{
    public string? BackupPath { get; set; }
    public string? WorldPath { get; set; }
    public string? Announcement { get; set; }
}

public class RollbackLog
{
    public string Time { get; set; } = "";
    public string BackupFileName { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string Status { get; set; } = "";
}

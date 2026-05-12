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
    private static object _lock = new object();
    private static RollbackStatus _currentStatus = new RollbackStatus();

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

    private T? GetPropertyValue<T>(object obj, params string[] propertyNames) where T : class
    {
        if (obj == null) return null;
        
        foreach (var name in propertyNames)
        {
            var prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value is T typedValue)
                {
                    return typedValue;
                }
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
        
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            if (!method.Name.StartsWith("get_") && !method.Name.StartsWith("set_"))
            {
                SDK.MSLX.Logger.Info($"  Method: {method.Name}");
            }
        }
    }

    private object? GetServerInstance()
    {
        var servers = SDK.MSLX.Config.Servers.GetServerList();
        if (servers == null || !servers.Any())
        {
            SDK.MSLX.Logger.Error("No servers found");
            return null;
        }
        return servers.First();
    }

    [HttpGet("backups")]
    public IActionResult GetBackups()
    {
        try
        {
            SDK.MSLX.Logger.Info("=== GetBackups called ===");
            
            var server = GetServerInstance();
            if (server == null)
            {
                return Ok(new ApiResponse<List<BackupInfo>>
                {
                    Code = 200,
                    Message = "无法获取服务器列表",
                    Data = new List<BackupInfo>()
                });
            }
            
            LogObjectProperties(server, "Server");
            
            var serverPath = GetPropertyValue(server, "Base", "ServerPath", "Path", "InstancePath", "ServerDir", "Dir", "WorkingDirectory", "ServerFolder", "BasePath", "RootPath", "HomePath", "ServerRoot", "InstallPath", "ExecutablePath");
            var backupPath = GetPropertyValue(server, "BackupPath", "BackupDir", "BackupFolder");
            
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
            
            var server = GetServerInstance();
            if (server == null)
            {
                return Ok(new ApiResponse<ServerInfo>
                {
                    Code = 200,
                    Message = "无法获取服务器列表",
                    Data = null
                });
            }
            
            LogObjectProperties(server, "Server");
            
            var serverPath = GetPropertyValue(server, "Base", "ServerPath", "Path", "InstancePath", "ServerDir", "Dir", "WorkingDirectory", "ServerFolder", "BasePath", "RootPath", "HomePath", "ServerRoot", "InstallPath", "ExecutablePath");
            var serverId = GetPropertyValue(server, "Id", "ServerId", "InstanceId", "Guid", "ServerGuid");
            var serverName = GetPropertyValue(server, "Name", "ServerName", "InstanceName", "DisplayName", "Title");
            var backupPath = GetPropertyValue(server, "BackupPath", "BackupDir", "BackupFolder");
            
            bool isRunning = CheckServerStatus(server);
            int playerCount = GetPlayerCount(server);
            
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

    private bool CheckServerStatus(object server)
    {
        try
        {
            var serverType = server.GetType();
            
            var isRunningProp = serverType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (isRunningProp != null)
            {
                var isRunningValue = isRunningProp.GetValue(server);
                if (isRunningValue is bool boolValue)
                {
                    SDK.MSLX.Logger.Info($"Got IsRunning from property: {boolValue}");
                    return boolValue;
                }
            }
            
            var statusProp = serverType.GetProperty("Status", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (statusProp != null)
            {
                var statusValue = statusProp.GetValue(server);
                SDK.MSLX.Logger.Info($"Status property value: {statusValue} (type: {statusValue?.GetType().Name})");
                if (statusValue != null)
                {
                    string statusStr = statusValue.ToString()?.ToLower() ?? string.Empty;
                    bool isRunning = statusStr == "running" || statusStr == "started" || statusStr == "active";
                    SDK.MSLX.Logger.Info($"Parsed status '{statusStr}' as running: {isRunning}");
                    return isRunning;
                }
            }
            
            var stateProp = serverType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (stateProp != null)
            {
                var stateValue = stateProp.GetValue(server);
                SDK.MSLX.Logger.Info($"State property value: {stateValue}");
                if (stateValue != null)
                {
                    string stateStr = stateValue.ToString()?.ToLower() ?? string.Empty;
                    bool isRunning = stateStr == "running" || stateStr == "started" || stateStr == "active";
                    SDK.MSLX.Logger.Info($"Parsed state '{stateStr}' as running: {isRunning}");
                    return isRunning;
                }
            }

            try
            {
                var method = serverType.GetMethod("IsServerRunning", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    var result = method.Invoke(server, null);
                    if (result is bool running)
                    {
                        SDK.MSLX.Logger.Info($"Got IsServerRunning from method: {running}");
                        return running;
                    }
                }
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Info($"Failed to call IsServerRunning method: {ex.Message}");
            }

            SDK.MSLX.Logger.Info("Could not determine server status, defaulting to false");
            return false;
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"CheckServerStatus failed: {ex}");
            return false;
        }
    }

    private int GetPlayerCount(object server)
    {
        try
        {
            var serverType = server.GetType();
            
            var onlinePlayerCountProp = serverType.GetProperty("OnlinePlayerCount", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (onlinePlayerCountProp != null)
            {
                var countValue = onlinePlayerCountProp.GetValue(server);
                if (countValue is int intValue)
                {
                    SDK.MSLX.Logger.Info($"Got OnlinePlayerCount from property: {intValue}");
                    return intValue;
                }
            }
            
            var playerCountProp = serverType.GetProperty("PlayerCount", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (playerCountProp != null)
            {
                var countValue = playerCountProp.GetValue(server);
                if (countValue is int intValue)
                {
                    SDK.MSLX.Logger.Info($"Got PlayerCount from property: {intValue}");
                    return intValue;
                }
            }
            
            var playersProp = serverType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (playersProp != null)
            {
                var playersValue = playersProp.GetValue(server);
                if (playersValue != null)
                {
                    var countMethod = playersValue.GetType().GetMethod("get_Count");
                    if (countMethod != null)
                    {
                        var countResult = countMethod.Invoke(playersValue, null);
                        if (countResult is int intValue)
                        {
                            SDK.MSLX.Logger.Info($"Got player count from Players collection: {intValue}");
                            return intValue;
                        }
                    }
                }
            }

            SDK.MSLX.Logger.Info("Could not get player count, defaulting to 0");
            return 0;
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"GetPlayerCount failed: {ex}");
            return 0;
        }
    }

    [HttpPost("stop-server")]
    public IActionResult StopServer()
    {
        try
        {
            SDK.MSLX.Logger.Info("=== StopServer called ===");
            
            var server = GetServerInstance();
            if (server == null)
            {
                return Ok(new ApiResponse<string>
                {
                    Code = 400,
                    Message = "无法获取服务器实例",
                    Data = null
                });
            }

            bool isRunning = CheckServerStatus(server);
            if (!isRunning)
            {
                return Ok(new ApiResponse<string>
                {
                    Code = 200,
                    Message = "服务器已经停止",
                    Data = "服务器未运行"
                });
            }

            var serverType = server.GetType();
            
            var stopMethod = serverType.GetMethod("Stop", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (stopMethod == null)
            {
                stopMethod = serverType.GetMethod("StopServer", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }
            if (stopMethod == null)
            {
                stopMethod = serverType.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }

            if (stopMethod != null)
            {
                SDK.MSLX.Logger.Info($"Calling stop method: {stopMethod.Name}");
                stopMethod.Invoke(server, null);
                
                int waitCount = 0;
                while (waitCount < 30)
                {
                    System.Threading.Thread.Sleep(1000);
                    isRunning = CheckServerStatus(server);
                    if (!isRunning) break;
                    waitCount++;
                    SDK.MSLX.Logger.Info($"Waiting for server to stop... ({waitCount}/30)");
                }
                
                if (!isRunning)
                {
                    SDK.MSLX.Logger.Info("Server stopped successfully");
                    return Ok(new ApiResponse<string>
                    {
                        Code = 200,
                        Message = "服务器已停止",
                        Data = "服务器停止成功"
                    });
                }
                else
                {
                    SDK.MSLX.Logger.Error("Server did not stop within timeout");
                    return Ok(new ApiResponse<string>
                    {
                        Code = 200,
                        Message = "服务器停止超时",
                        Data = "服务器正在停止中，请稍后检查"
                    });
                }
            }
            else
            {
                SDK.MSLX.Logger.Error("No stop method found on server object");
                return Ok(new ApiResponse<string>
                {
                    Code = 500,
                    Message = "服务器不支持停止操作",
                    Data = null
                });
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"StopServer failed: {ex}");
            return Ok(new ApiResponse<string>
            {
                Code = 500,
                Message = $"停止服务器失败: {ex.Message}",
                Data = null
            });
        }
    }

    [HttpPost("start-server")]
    public IActionResult StartServer()
    {
        try
        {
            SDK.MSLX.Logger.Info("=== StartServer called ===");
            
            var server = GetServerInstance();
            if (server == null)
            {
                return Ok(new ApiResponse<string>
                {
                    Code = 400,
                    Message = "无法获取服务器实例",
                    Data = null
                });
            }

            bool isRunning = CheckServerStatus(server);
            if (isRunning)
            {
                return Ok(new ApiResponse<string>
                {
                    Code = 200,
                    Message = "服务器已经运行",
                    Data = "服务器正在运行"
                });
            }

            var serverType = server.GetType();
            
            var startMethod = serverType.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (startMethod == null)
            {
                startMethod = serverType.GetMethod("StartServer", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }

            if (startMethod != null)
            {
                SDK.MSLX.Logger.Info($"Calling start method: {startMethod.Name}");
                startMethod.Invoke(server, null);
                
                int waitCount = 0;
                while (waitCount < 60)
                {
                    System.Threading.Thread.Sleep(1000);
                    isRunning = CheckServerStatus(server);
                    if (isRunning) break;
                    waitCount++;
                    SDK.MSLX.Logger.Info($"Waiting for server to start... ({waitCount}/60)");
                }
                
                if (isRunning)
                {
                    SDK.MSLX.Logger.Info("Server started successfully");
                    return Ok(new ApiResponse<string>
                    {
                        Code = 200,
                        Message = "服务器已启动",
                        Data = "服务器启动成功"
                    });
                }
                else
                {
                    SDK.MSLX.Logger.Error("Server did not start within timeout");
                    return Ok(new ApiResponse<string>
                    {
                        Code = 200,
                        Message = "服务器启动超时",
                        Data = "服务器正在启动中，请稍后检查"
                    });
                }
            }
            else
            {
                SDK.MSLX.Logger.Error("No start method found on server object");
                return Ok(new ApiResponse<string>
                {
                    Code = 500,
                    Message = "服务器不支持启动操作",
                    Data = null
                });
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"StartServer failed: {ex}");
            return Ok(new ApiResponse<string>
            {
                Code = 500,
                Message = $"启动服务器失败: {ex.Message}",
                Data = null
            });
        }
    }

    private bool SendAnnouncement(object server, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
            {
                SDK.MSLX.Logger.Info("No announcement message provided, skipping");
                return true;
            }
            
            SDK.MSLX.Logger.Info($"Sending announcement: {message}");
            
            var serverType = server.GetType();
            
            var sendMessageMethod = serverType.GetMethod("SendMessage", BindingFlags.Public | BindingFlags.Instance);
            if (sendMessageMethod != null)
            {
                var parameters = sendMessageMethod.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    sendMessageMethod.Invoke(server, new object[] { message });
                    SDK.MSLX.Logger.Info("Announcement sent successfully via SendMessage");
                    return true;
                }
            }
            
            var broadcastMethod = serverType.GetMethod("Broadcast", BindingFlags.Public | BindingFlags.Instance);
            if (broadcastMethod != null)
            {
                var parameters = broadcastMethod.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    broadcastMethod.Invoke(server, new object[] { message });
                    SDK.MSLX.Logger.Info("Announcement sent successfully via Broadcast");
                    return true;
                }
            }
            
            var executeCommandMethod = serverType.GetMethod("ExecuteCommand", BindingFlags.Public | BindingFlags.Instance);
            if (executeCommandMethod != null)
            {
                var parameters = executeCommandMethod.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    string command = $"say {message}";
                    executeCommandMethod.Invoke(server, new object[] { command });
                    SDK.MSLX.Logger.Info("Announcement sent successfully via ExecuteCommand");
                    return true;
                }
            }
            
            SDK.MSLX.Logger.Info("No announcement method found, skipping announcement");
            return true;
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"SendAnnouncement failed: {ex}");
            return false;
        }
    }

    [HttpGet("rollback-status")]
    public IActionResult GetRollbackStatus()
    {
        return Ok(new ApiResponse<RollbackStatus>
        {
            Code = 200,
            Message = "success",
            Data = _currentStatus
        });
    }

    [HttpPost("execute")]
    public IActionResult ExecuteRollback([FromBody] RollbackRequest request)
    {
        if (!Monitor.TryEnter(_lock, 0))
        {
            return Ok(new ApiResponse<string>
            {
                Code = 400,
                Message = "正在执行回档操作，请稍后再试",
                Data = null
            });
        }

        try
        {
            SDK.MSLX.Logger.Info("=== ExecuteRollback called ===");
            SDK.MSLX.Logger.Info($"Request: {JsonSerializer.Serialize(request)}");
            
            UpdateRollbackStatus("initializing", "正在初始化回档操作...", 0);
            
            if (string.IsNullOrEmpty(request.BackupPath) || !System.IO.File.Exists(request.BackupPath))
            {
                SDK.MSLX.Logger.Error($"Backup file not found: {request.BackupPath}");
                UpdateRollbackStatus("failed", "备份文件不存在", 0);
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
                UpdateRollbackStatus("failed", "存档路径不能为空", 0);
                return Ok(new ApiResponse<string>
                {
                    Code = 400,
                    Message = "存档路径不能为空",
                    Data = null
                });
            }

            var server = GetServerInstance();
            if (server == null)
            {
                SDK.MSLX.Logger.Error("Server instance is null");
                UpdateRollbackStatus("failed", "无法获取服务器实例", 0);
                return Ok(new ApiResponse<string>
                {
                    Code = 500,
                    Message = "无法获取服务器实例",
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
                UpdateRollbackStatus("checking_server", "正在检查服务器状态...", 5);
                
                bool wasRunning = CheckServerStatus(server);
                
                if (wasRunning)
                {
                    UpdateRollbackStatus("sending_announcement", "正在发送回档公告...", 10);
                    
                    if (!string.IsNullOrEmpty(request.Announcement))
                    {
                        SendAnnouncement(server, request.Announcement);
                        System.Threading.Thread.Sleep(2000);
                    }
                    
                    UpdateRollbackStatus("stopping_server", "正在停止服务器...", 15);
                    
                    StopServerInternal(server);
                    System.Threading.Thread.Sleep(3000);
                }

                UpdateRollbackStatus("backing_up", "正在备份当前存档...", 25);
                
                if (Directory.Exists(request.WorldPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var backupDir = $"{request.WorldPath}_backup_{timestamp}";
                    SDK.MSLX.Logger.Info($"Backing up existing world to: {backupDir}");
                    Directory.Move(request.WorldPath, backupDir);
                }

                UpdateRollbackStatus("extracting", "正在解压备份文件...", 50);
                
                SDK.MSLX.Logger.Info($"Extracting backup: {request.BackupPath} -> {request.WorldPath}");
                Directory.CreateDirectory(request.WorldPath);
                ZipFile.ExtractToDirectory(request.BackupPath, request.WorldPath);

                UpdateRollbackStatus("restoring", "正在恢复服务器...", 75);
                
                if (wasRunning)
                {
                    StartServerInternal(server);
                }

                UpdateRollbackStatus("completed", "回档完成", 100);
                
                logEntry.Status = "success";
                SDK.MSLX.Logger.Info("Rollback completed successfully");
                SaveLog(logEntry);

                return Ok(new ApiResponse<string>
                {
                    Code = 200,
                    Message = "回档成功",
                    Data = wasRunning ? "回档操作已完成，服务器正在重启..." : "回档操作已完成"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                SDK.MSLX.Logger.Error($"Permission denied: {ex}");
                logEntry.Status = "failed";
                SaveLog(logEntry);
                UpdateRollbackStatus("failed", $"权限不足: {ex.Message}", 0);
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
                UpdateRollbackStatus("failed", $"回档失败: {ex.Message}", 0);
                return Ok(new ApiResponse<string>
                {
                    Code = 500,
                    Message = $"回档失败: {ex.Message}",
                    Data = null
                });
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    private void UpdateRollbackStatus(string status, string message, int progress)
    {
        _currentStatus.Status = status;
        _currentStatus.Message = message;
        _currentStatus.Progress = progress;
        _currentStatus.LastUpdate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SDK.MSLX.Logger.Info($"Rollback status updated: {status} - {message} ({progress}%)");
    }

    private void StopServerInternal(object server)
    {
        try
        {
            var serverType = server.GetType();
            
            var stopMethod = serverType.GetMethod("Stop", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (stopMethod == null)
            {
                stopMethod = serverType.GetMethod("StopServer", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }
            if (stopMethod == null)
            {
                stopMethod = serverType.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }

            if (stopMethod != null)
            {
                SDK.MSLX.Logger.Info($"Calling stop method: {stopMethod.Name}");
                stopMethod.Invoke(server, null);
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"StopServerInternal failed: {ex}");
        }
    }

    private void StartServerInternal(object server)
    {
        try
        {
            var serverType = server.GetType();
            
            var startMethod = serverType.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (startMethod == null)
            {
                startMethod = serverType.GetMethod("StartServer", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            }

            if (startMethod != null)
            {
                SDK.MSLX.Logger.Info($"Calling start method: {startMethod.Name}");
                startMethod.Invoke(server, null);
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"StartServerInternal failed: {ex}");
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
    public bool AutoRestart { get; set; } = true;
}

public class RollbackLog
{
    public string Time { get; set; } = "";
    public string BackupFileName { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string Status { get; set; } = "";
}

public class RollbackStatus
{
    public string Status { get; set; } = "idle";
    public string Message { get; set; } = "就绪";
    public int Progress { get; set; } = 0;
    public string LastUpdate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

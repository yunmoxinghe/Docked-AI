# 统一日志服务使用指南

## 概述

`LogService` 是 DockedTools 的统一日志管理系统，用于记录应用程序运行时的错误、警告和重要信息。

## 特性

- ✅ 自动写入本地日志文件
- ✅ 同时输出到调试控制台
- ✅ 支持多种日志级别（Debug, Info, Warning, Error）
- ✅ 自动记录调用位置（文件名、行号、方法名）
- ✅ 详细的异常信息记录（包括内部异常）
- ✅ 线程安全的文件写入
- ✅ 自动清理旧日志

## 日志级别

### Debug（调试）
- 仅在 Debug 模式下输出到控制台
- 不写入文件
- 用于开发过程中的调试信息

### Info（信息）
- 记录重要的操作和状态变化
- 写入 `app.log`

### Warning（警告）
- 记录可能的问题，但不影响功能
- 写入 `app.log`

### Error（错误）
- 记录错误和异常
- 写入 `error.log`

## 使用方法

### 1. 引入命名空间

```csharp
using DockedTools.Features.UnifiedCalls.Logging;
```

### 2. 记录错误（带异常）

```csharp
try
{
    // 你的代码
}
catch (Exception ex)
{
    LogService.Error("模块名", "操作描述", ex);
}
```

**示例：**
```csharp
try
{
    await LoadDataAsync();
}
catch (Exception ex)
{
    LogService.Error("SettingsPage", "加载设置失败", ex);
}
```

### 3. 记录错误（无异常）

```csharp
if (result == null)
{
    LogService.Error("DataService", "获取数据返回 null");
    return;
}
```

### 4. 记录警告

```csharp
if (cache.IsStale)
{
    LogService.Warning("CacheService", "缓存已过期，正在刷新");
    await RefreshCacheAsync();
}
```

### 5. 记录信息

```csharp
LogService.Info("AppStartup", "应用程序启动完成");
```

### 6. 记录调试信息

```csharp
LogService.Debug("DataProcessor", $"处理了 {count} 条记录");
```

## 日志格式

日志会自动包含以下信息：

```
[2026-06-10 14:30:25.123] [Error] [SettingsPage] 加载设置失败
异常类型: System.IO.FileNotFoundException
异常消息: Could not find file 'settings.json'
堆栈跟踪:
   at System.IO.__Error.WinIOError(Int32 errorCode, String maybeFullPath)
   at System.IO.FileStream.Init(String path, FileMode mode, ...)
位置: 设置页面.xaml.cs:123 (LoadSettingsAsync)
```

## 日志文件位置

日志文件保存在应用数据目录：

```
%LOCALAPPDATA%\Packages\[PackageFamily]\LocalState\logs\
```

**日志文件：**
- `app.log` - 包含 Info 和 Warning 级别的日志
- `error.log` - 仅包含 Error 级别的日志

## 最佳实践

### ✅ 推荐做法

```csharp
// 1. 记录关键操作的开始和结束
LogService.Info("DataSync", "开始同步数据");
await SyncDataAsync();
LogService.Info("DataSync", "数据同步完成");

// 2. 捕获并记录异常
try
{
    await SaveAsync();
}
catch (Exception ex)
{
    LogService.Error("DataService", "保存数据失败", ex);
    // 根据需要决定是否重新抛出
    throw;
}

// 3. 记录警告但继续执行
if (!ValidateData(data))
{
    LogService.Warning("Validator", "数据验证失败，使用默认值");
    data = GetDefaultData();
}
```

### ❌ 不推荐做法

```csharp
// 1. 不要只记录异常而不处理
catch (Exception ex)
{
    LogService.Error("Module", "错误", ex);
    // 应该考虑如何恢复或通知用户
}

// 2. 不要记录过多调试信息
for (int i = 0; i < 1000000; i++)
{
    LogService.Debug("Loop", $"处理第 {i} 项"); // 太多了！
}

// 3. 不要在循环中记录错误
foreach (var item in items)
{
    if (!ProcessItem(item))
    {
        LogService.Error("Processor", "处理失败"); // 应该汇总统计
    }
}
```

## 日志清理

应用启动时会自动清理 7 天前的旧日志：

```csharp
// 在应用启动时调用
LogService.CleanupOldLogs(7); // 保留最近 7 天
```

可以根据需要调整保留天数：

```csharp
LogService.CleanupOldLogs(30); // 保留最近 30 天
```

## 获取日志目录

```csharp
var logDir = LogService.GetLogDirectory();
if (logDir != null)
{
    Console.WriteLine($"日志目录: {logDir}");
}
```

## 替换旧的日志代码

### 旧代码（不推荐）

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"[Module] Error: {ex.Message}");
}
```

### 新代码（推荐）

```csharp
catch (Exception ex)
{
    LogService.Error("Module", "操作描述", ex);
}
```

## 性能考虑

- 日志服务使用异步 I/O 和文件锁保证线程安全
- Debug 级别日志在 Release 模式下完全不执行
- 日志写入失败不会影响程序运行
- 自动清理旧日志防止磁盘占用过多

## 故障排除

### 日志文件没有创建

如果应用数据目录不可访问，日志服务会：
1. 尝试使用临时目录 `%TEMP%\DockedAI_Logs\`
2. 如果也失败，只输出到调试控制台

### 查看日志

**开发模式：**
- 使用 Visual Studio 的"输出"窗口
- 或使用 `winapp run --debug-output`

**生产模式：**
- 打开日志文件目录查看 `.log` 文件
- 使用任何文本编辑器打开

## 示例：完整的错误处理

```csharp
public async Task<bool> SaveSettingsAsync()
{
    try
    {
        LogService.Info("Settings", "开始保存设置");
        
        var data = SerializeSettings();
        await File.WriteAllTextAsync(SettingsPath, data);
        
        LogService.Info("Settings", "设置保存成功");
        return true;
    }
    catch (UnauthorizedAccessException ex)
    {
        LogService.Error("Settings", "没有权限保存设置", ex);
        await ShowErrorDialogAsync("没有权限保存设置，请检查文件权限");
        return false;
    }
    catch (IOException ex)
    {
        LogService.Error("Settings", "保存设置时发生 I/O 错误", ex);
        await ShowErrorDialogAsync("保存设置失败，请重试");
        return false;
    }
    catch (Exception ex)
    {
        LogService.Error("Settings", "保存设置时发生未知错误", ex);
        await ShowErrorDialogAsync("发生未知错误，请查看日志");
        return false;
    }
}
```

## 更新日期

2026-06-10

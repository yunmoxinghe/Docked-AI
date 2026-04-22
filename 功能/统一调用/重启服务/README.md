# 重启服务 (AppRestartService)

应用重启服务，专为 WinUI 3 无包应用设计，完美适配单实例架构。

## 核心原理

WinUI 3 无包应用不能像 UWP 那样使用 `CoreApplication.RequestRestartAsync()`，必须：
1. 手动启动新的 exe 实例
2. 传递 `--restart` 参数绕过单实例检测
3. 新实例等待旧实例退出并释放 Mutex
4. 旧实例调用 `Application.Current.Exit()`

## 快速使用

### 1. 基础重启
```csharp
using Docked_AI.功能.统一调用;

// 简单重启
AppRestartService.Restart();
```

### 2. 带参数重启
```csharp
// 更新后重启
AppRestartService.RestartWithArgs("--restart-from=update");

// 设置变更后重启
AppRestartService.RestartWithArgs("--restart-from=settings");

// 崩溃恢复重启
AppRestartService.RestartWithArgs("--restart-from=crash");
```

### 3. 延迟重启（保存状态）
```csharp
// 先执行回调（同步），然后延迟 500ms，最后重启（仅支持基础重启）
AppRestartService.RestartWithDelay(500, () => {
    // ⚠️ 注意：回调必须是同步操作，异步方法不会等待完成
    SaveSettings();  // 确保这是同步方法
    SaveWindowState();
});

// 如需异步保存状态或带参数重启，建议手动实现：
await SaveSettingsAsync();
await Task.Delay(500);
AppRestartService.RestartWithArgs("--restart-from=update");
```

### 4. 管理员权限重启
```csharp
// 以管理员权限重启（会弹出 UAC 提示）
AppRestartService.RestartAsAdmin();
```

## 检测重启来源

在 `App.xaml.cs` 的 `OnLaunched` 中：

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    // 检查是否从重启启动
    if (AppRestartService.IsRestartedLaunch())
    {
        var source = AppRestartService.GetRestartSource();
        
        switch (source)
        {
            case "update":
                // 显示"更新完成"提示
                ShowUpdateCompleteNotification();
                break;
                
            case "settings":
                // 直接打开设置页
                NavigateToSettings();
                break;
                
            case "crash":
                // 显示崩溃恢复提示
                ShowCrashRecoveryDialog();
                break;
        }
    }
}
```

## API 参考

### 方法

| 方法 | 说明 |
|------|------|
| `Restart()` | 基础重启 |
| `RestartWithArgs(params string[] args)` | 带参数重启 |
| `RestartAsAdmin()` | 以管理员权限重启 |
| `RestartWithDelay(int ms, Action? callback)` | 先执行回调（同步），延迟指定毫秒后重启（仅基础重启） |
| `IsRestartedLaunch()` | 检查是否从重启启动 |
| `GetRestartSource()` | 获取重启来源（如 "update", "crash" 等） |

### 参数格式

重启参数会自动添加 `--restart` 标记，你只需要传递业务参数：

```csharp
// ✅ 推荐写法
RestartWithArgs("--restart-from=update");

// ❌ 不需要手动添加 --restart
RestartWithArgs("--restart", "--restart-from=update");
```

## 单实例适配说明

本服务已完美适配应用的单实例架构：

1. **自动绕过拦截**：所有重启方法都会传递 `--restart` 参数
2. **智能等待**：新实例会等待旧实例退出（最多 3 秒）
3. **超时保护**：如果旧实例未正常退出，强制成为主实例

## 常见场景

### 场景 1：设置页添加重启按钮
```csharp
private void RestartButton_Click(object sender, RoutedEventArgs e)
{
    // 方式 1：同步保存（适合简单配置）
    AppRestartService.RestartWithDelay(300, () => {
        SettingsManager.Save();  // 必须是同步方法
    });
}

private async void RestartButton_Click_Async(object sender, RoutedEventArgs e)
{
    // 方式 2：异步保存（推荐，适合复杂操作）
    await SettingsManager.SaveAsync();
    await Task.Delay(300);
    AppRestartService.Restart();
}
```

### 场景 2：更新完成后重启
```csharp
private async Task ApplyUpdateAsync()
{
    await DownloadAndInstallUpdate();
    
    // 保存状态后重启并标记来源
    SaveSettings();
    await Task.Delay(300); // 确保保存完成
    AppRestartService.RestartWithArgs("--restart-from=update");
}
```

### 场景 3：崩溃恢复
```csharp
private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    // 记录崩溃日志
    LogCrash(e.Exception);
    
    // 重启恢复
    AppRestartService.RestartWithArgs("--restart-from=crash");
}
```

## 注意事项

⚠️ **状态丢失**：重启会丢失内存中的所有状态，务必在重启前保存重要数据

⚠️ **同步回调限制**：`RestartWithDelay` 的回调是同步执行的，如果需要异步保存状态，请手动实现延迟重启

⚠️ **权限变化**：`RestartAsAdmin()` 会提升权限，可能导致文件访问路径变化

⚠️ **单实例冲突**：如果修改了单实例逻辑，确保 `--restart` 参数能被正确识别

⚠️ **执行顺序**：`RestartWithDelay` 的执行顺序是：回调 → 延迟 → 重启，延迟时间不是给回调预留的

## 技术细节

- **进程启动**：使用 `Process.Start` + `UseShellExecute = true`
- **单实例检测**：在 `App` 构造函数中通过 `--restart` 参数绕过 Mutex 检测
- **等待机制**：新实例轮询 Mutex（100ms 间隔，最多 30 次）
- **退出方式**：`Application.Current.Exit()` 确保资源正确释放

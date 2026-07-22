# AsyncSafety - 异步安全 Helper

## 概述

`AsyncSafety` 是一个轻量级的异步安全包装工具，用于处理 WinUI 3 应用中的异步事件处理器，避免 `async void` 异常逃逸到 XAML 未处理异常路径。

## 主要功能

1. **包装 async void 事件处理器** - 捕获并记录异常，避免异常终止应用
2. **统一异常记录** - 所有异常自动通过 `LogService` 记录
3. **保持 WinUI 签名要求** - 事件处理器保持 `void` 签名，内部逻辑使用 `async Task`

## 使用方法

### 1. 基本用法 - async void 入口

```csharp
using DockedTools.Features.UnifiedCalls.AsyncSafety;

// WinUI 事件处理器（保持 void 签名）
private void Button_Click(object sender, RoutedEventArgs e)
{
    // 委托到安全包装器
    AsyncSafety.Run(ButtonClickAsync, "ModuleName", "ButtonClick");
}

// 实际异步逻辑
private async Task ButtonClickAsync()
{
    await SomeAsyncOperation();
    // 如果这里抛出异常，会被 AsyncSafety 捕获并记录
}
```

### 2. async Task 版本（可等待）

```csharp
// 可等待的包装器，异常会重新抛出
public async Task SaveDataAsync()
{
    try
    {
        await AsyncSafety.RunTask(
            SaveDataInternalAsync,
            "DataService",
            "SaveData"
        );
    }
    catch (Exception ex)
    {
        // 可以在这里处理异常
        ShowErrorMessage(ex.Message);
    }
}
```

### 3. 带返回值的版本

```csharp
// 异常时返回默认值
private async Task<int> GetUserCountAsync()
{
    return await AsyncSafety.RunTask(
        async () => await database.GetCountAsync(),
        "UserService",
        "GetUserCount",
        defaultValue: 0  // 异常时返回 0
    );
}
```

### 4. DispatcherQueue 包装

```csharp
// 安全的 DispatcherQueue.TryEnqueue
AsyncSafety.TryEnqueue(
    DispatcherQueue,
    async () => await UpdateUIAsync(),
    "UIService",
    "UpdateUI"
);
```

## 设计原则

### 为什么需要 AsyncSafety？

在 WinUI 3 应用中，事件处理器必须是 `void` 签名：

```csharp
// ❌ 错误：事件处理器不能是 async Task
private async Task Button_Click(object sender, RoutedEventArgs e)
{
    // 编译错误：签名不匹配
}

// ✅ 正确：但异常会逃逸
private async void Button_Click(object sender, RoutedEventArgs e)
{
    await SomeOperation();
    throw new Exception("这个异常会直接进入 XAML 未处理异常路径！");
}

// ✅ 最佳实践：使用 AsyncSafety
private void Button_Click(object sender, RoutedEventArgs e)
{
    AsyncSafety.Run(ButtonClickAsync, "Module", "Operation");
}

private async Task ButtonClickAsync()
{
    // 异常会被安全捕获和记录
}
```

### 异常处理策略

- **Run (async void)**: 捕获并记录异常，**不会重新抛出**
- **RunTask (async Task)**: 捕获、记录，然后**重新抛出**异常
- **RunTask&lt;T&gt;**: 捕获并记录异常，**返回默认值**

## 高风险场景

以下场景必须使用 AsyncSafety 包装：

1. **所有 XAML 事件处理器** (Button_Click, Page_Loaded 等)
2. **WebView2 事件** (NavigationCompleted, WebMessageReceived 等)
3. **窗口生命周期事件** (Window_Closed, StateChanged 等)
4. **DispatcherQueue.TryEnqueue 中的 async lambda**
5. **任何 async void 方法**

## 日志输出

所有异常都会通过 `LogService.Error` 记录到 `logs/error.log`：

```
[2026-06-20 10:30:45.123] [Error] [WebBrowserPage] 异步操作失败: NavigationCompleted
异常类型: System.NullReferenceException
异常消息: Object reference not set to an instance of an object.
堆栈跟踪:
   at WebBrowserPage.HandleNavigationAsync() in WebBrowserPage.xaml.cs:line 1234
位置: WebBrowserPage.xaml.cs:1234 (CoreWebView2_NavigationCompleted)
```

## 性能考虑

- **零额外分配** - 使用委托传递，不创建额外的 Task 包装
- **CallerAttributes** - 调用位置信息在编译时确定，无运行时开销
- **ConfigureAwait(true)** - WinUI 应用通常需要返回 UI 线程，避免 SynchronizationContext 切换问题

## 相关设计文档

- [质量稳定性修复设计文档](../../../.kiro/specs/quality-stability-fixes/design.md) - 章节 5.2
- [任务文档](../../../.kiro/specs/quality-stability-fixes/tasks.md) - 任务 1.2

## 使用示例

项目中已改造的示例：

- `功能/主窗口/入口/主窗口.xaml.cs` - `ShowSplash`
- `功能/页面/网页应用/网页浏览/网页浏览页面.xaml.cs` - WebView2 事件处理器
- `功能/主窗口/显示隐藏/窗口宿主控制器.cs` - `OnWindowStateChanged`

---

**最后更新**: 2026-06-20

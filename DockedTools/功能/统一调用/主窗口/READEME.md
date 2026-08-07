# 主窗口统一调用服务

提供主窗口状态管理的统一调用接口。

## 功能概述

1. **状态查询服务**：检查当前窗口状态（显示/隐藏、固定/窗口化、最大化等）
2. **状态变更服务**：申请改变窗口状态（显示/隐藏、固定/取消固定、最大化/还原）
3. **状态通知服务**：订阅窗口状态变化事件

## 使用示例

### 状态查询

```csharp
using DockedTools.Features.UnifiedCalls.MainWindow;

// 获取当前状态
var currentState = MainWindowService.CurrentState;

// 检查窗口状态
bool isVisible = MainWindowService.IsVisible;    // 窗口是否可见
bool isPinned = MainWindowService.IsPinned;      // 是否处于固定模式
bool isMaximized = MainWindowService.IsMaximized; // 是否最大化
bool isWindowed = MainWindowService.IsWindowed;   // 是否窗口化模式
bool isHidden = MainWindowService.IsHidden;       // 是否隐藏
```

### 状态变更请求

```csharp
// 切换状态
MainWindowService.RequestToggleWindow();    // 切换显示/隐藏
MainWindowService.RequestTogglePinned();    // 切换固定/取消固定
MainWindowService.RequestToggleMaximize();  // 切换最大化/还原

// 设置特定状态
MainWindowService.RequestShow();      // 显示窗口
MainWindowService.RequestHide();      // 隐藏窗口
MainWindowService.RequestPin();       // 固定窗口
MainWindowService.RequestUnpin();     // 取消固定
MainWindowService.RequestMaximize();  // 最大化窗口
MainWindowService.RequestRestore();   // 还原窗口
```

### 状态变化通知

```csharp
// 订阅状态变化事件
MainWindowService.StateChanged += OnWindowStateChanged;

private void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
{
    Console.WriteLine($"窗口状态变化：{args.PreviousState} -> {args.CurrentState}");
    Console.WriteLine($"变化原因：{args.Reason}");
    Console.WriteLine($"变化时间：{args.Timestamp}");
}

// 取消订阅
MainWindowService.StateChanged -= OnWindowStateChanged;
```

## 窗口状态说明

- **NotCreated**：窗口尚未创建
- **Hidden**：窗口已隐藏
- **Windowed**：窗口化模式（标准停靠）
- **Maximized**：最大化模式
- **Pinned**：固定模式（AppBar）

## 架构设计

- 服务作为全局单例，通过静态方法访问
- 不直接持有窗口引用，而是通过 `IWindowController` 接口解耦
- 状态管理器（`WindowStateManager`）负责状态逻辑
- 窗口控制器（`WindowHostController`）负责执行动作
- 视图模型（`MainWindowViewModel`）负责 UI 绑定和属性通知

## 实现文件

- `MainWindowService.cs`：服务主文件
- `IWindowController.cs`：窗口控制器接口（包含在服务文件中）

## 注册机制

服务在主窗口初始化时自动注册：

```csharp
// 主窗口构造函数中
MainWindowService.Register(_windowController, _viewModel);

// 主窗口关闭时
MainWindowService.Unregister();
```

## 注意事项

1. 所有状态变更请求都是异步执行的，不会阻塞调用线程
2. 状态变更可能失败（例如正在执行另一个转换），需要通过事件或状态查询确认结果
3. 订阅 StateChanged 事件时，记得在不需要时取消订阅，避免内存泄漏

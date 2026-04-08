# UI 集成指南

本文档提供了将新的 WindowStateManager 架构集成到现有 UI 控件的详细指南。

## 概述

新架构已经在 `MainWindow.xaml.cs` 中初始化，现在需要将现有的 UI 控件（按钮、托盘图标、键盘快捷键）连接到新的状态管理系统。

## 任务 13.1: 更新全屏按钮处理程序

### 需求
- 在按钮点击处理程序中检查当前状态
- 如果是 Fullscreen，调用 TransitionTo(Floating)
- 否则，调用 TransitionTo(Fullscreen)

### 实现步骤

1. 找到全屏按钮的点击事件处理器（可能在 Linker 或 NavBar 中）
2. 修改处理器以使用新的 WindowStateManager

```csharp
// 在 MainWindow.xaml.cs 中添加方法
public void ToggleFullscreen()
{
    if (_stateManager == null)
    {
        // 回退到旧架构
        _windowController.ToggleMaximize();
        return;
    }
    
    // 使用新架构
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen)
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen);
    }
}
```

3. 在 Linker 或 NavBar 的按钮点击事件中调用此方法

## 任务 13.2: 更新边栏按钮处理程序

### 需求
- 在按钮点击处理程序中检查当前状态
- 如果是 Sidebar，调用 TransitionTo(Floating)
- 否则，调用 TransitionTo(Sidebar)

### 实现步骤

```csharp
// 在 MainWindow.xaml.cs 中添加方法
public void ToggleSidebar()
{
    if (_stateManager == null)
    {
        // 回退到旧架构
        TogglePinnedDock();
        return;
    }
    
    // 使用新架构
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar)
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar);
    }
}
```

## 任务 13.3: 更新托盘图标处理程序

### 需求
- 在托盘图标点击处理程序中检查当前状态
- 如果是 Hidden，恢复到上次可见状态（隐藏前保存）
- 否则，调用 TransitionTo(Hidden) 并保存当前状态

### 实现步骤

1. 在 MainWindow 中添加字段保存上次可见状态：

```csharp
private Docked_AI.功能.主窗口v2.状态机.WindowState _lastVisibleState = 
    Docked_AI.功能.主窗口v2.状态机.WindowState.Floating;
```

2. 在 `OnWindowStateChanged` 事件处理器中保存可见状态：

```csharp
private void OnWindowStateChanged(
    Docked_AI.功能.主窗口v2.状态机.WindowState from, 
    Docked_AI.功能.主窗口v2.状态机.WindowState to)
{
    System.Diagnostics.Debug.WriteLine($"MainWindow: State changed from {from} to {to}");
    
    // 保存上次可见状态
    if (to != Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden &&
        to != Docked_AI.功能.主窗口v2.状态机.WindowState.Initializing)
    {
        _lastVisibleState = to;
    }
    
    // TODO: 更新 UI 以反映新状态
}
```

3. 添加切换窗口可见性的方法：

```csharp
public void ToggleWindowVisibility()
{
    if (_stateManager == null)
    {
        // 回退到旧架构
        ToggleWindow();
        return;
    }
    
    // 使用新架构
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden)
    {
        // 恢复到上次可见状态
        _stateManager.TransitionTo(_lastVisibleState);
    }
    else
    {
        // 隐藏窗口
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden);
    }
}
```

4. 在 TrayIconManager 中调用此方法（需要修改 `功能/托盘/` 中的代码）

## 任务 13.4: 更新键盘快捷键处理程序

### 需求
- 将现有快捷键映射到 TransitionTo 调用
- 确保 F11 切换全屏，Ctrl+Shift+S 切换边栏等

### 实现步骤

1. 找到键盘快捷键处理器（可能在 `功能/快捷键/` 中）
2. 将快捷键映射到新的方法：

```csharp
// 示例：在快捷键处理器中
switch (hotkey.Key)
{
    case VirtualKey.F11:
        mainWindow.ToggleFullscreen();
        break;
    case VirtualKey.S when hotkey.Modifiers.HasFlag(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift):
        mainWindow.ToggleSidebar();
        break;
    // ... 其他快捷键
}
```

## 任务 13.5: 添加转换状态 UI 反馈

### 需求
- 订阅 TransitionStarted 事件
- 当 TransitioningTo != null 时显示加载指示器或禁用按钮
- 订阅 StateChanged 事件
- 转换完成时隐藏加载指示器并更新按钮状态

### 实现步骤

1. 在 `OnWindowTransitionStarted` 中显示加载指示器：

```csharp
private void OnWindowTransitionStarted(
    Docked_AI.功能.主窗口v2.状态机.WindowState from, 
    Docked_AI.功能.主窗口v2.状态机.WindowState to)
{
    System.Diagnostics.Debug.WriteLine($"MainWindow: Transition started from {from} to {to}");
    
    // 禁用相关按钮，防止重复点击
    if (_linker != null)
    {
        _linker.NavBarInstance.SetButtonsEnabled(false);
    }
    
    // 显示加载指示器（可选）
    // ShowLoadingIndicator();
}
```

2. 在 `OnWindowStateChanged` 中隐藏加载指示器：

```csharp
private void OnWindowStateChanged(
    Docked_AI.功能.主窗口v2.状态机.WindowState from, 
    Docked_AI.功能.主窗口v2.状态机.WindowState to)
{
    System.Diagnostics.Debug.WriteLine($"MainWindow: State changed from {from} to {to}");
    
    // 保存上次可见状态
    if (to != Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden &&
        to != Docked_AI.功能.主窗口v2.状态机.WindowState.Initializing)
    {
        _lastVisibleState = to;
    }
    
    // 启用按钮
    if (_linker != null)
    {
        _linker.NavBarInstance.SetButtonsEnabled(true);
    }
    
    // 隐藏加载指示器（可选）
    // HideLoadingIndicator();
    
    // 更新按钮视觉状态
    UpdateButtonStates(to);
}
```

## 任务 13.6: 更新按钮视觉状态

### 需求
- 订阅 StateChanged 事件
- 根据 CurrentState 更新按钮文本/图标（例如，在 Fullscreen 时显示"退出全屏"）
- 根据 TransitioningTo 更新按钮启用状态

### 实现步骤

```csharp
private void UpdateButtonStates(Docked_AI.功能.主窗口v2.状态机.WindowState currentState)
{
    if (_linker == null) return;
    
    // 更新全屏按钮
    bool isFullscreen = currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen;
    _linker.NavBarInstance.UpdateWindowStateIcon(isFullscreen);
    
    // 更新边栏按钮
    bool isSidebar = currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar;
    _linker.NavBarInstance.UpdateDockToggleIcon(isSidebar);
    
    // 更新内容区域的圆角和边距
    _linker.UpdateContentCornerRadius(isSidebar);
    _linker.UpdateContentTopMargin(isFullscreen || isSidebar);
}
```

## 任务 13.7: 添加工具提示更新

### 需求
- 更新工具提示以反映当前状态和可用操作
- 当 TransitioningTo != null 时显示"请稍候，正在切换窗口模式"

### 实现步骤

```csharp
private void UpdateTooltips(Docked_AI.功能.主窗口v2.状态机.WindowState currentState)
{
    if (_linker == null) return;
    
    // 检查是否正在转换
    if (_stateManager?.TransitioningTo != null)
    {
        _linker.NavBarInstance.SetTooltip("请稍候，正在切换窗口模式...");
        return;
    }
    
    // 根据当前状态更新工具提示
    switch (currentState)
    {
        case Docked_AI.功能.主窗口v2.状态机.WindowState.Floating:
            _linker.NavBarInstance.SetFullscreenTooltip("进入全屏 (F11)");
            _linker.NavBarInstance.SetSidebarTooltip("停靠到边栏 (Ctrl+Shift+S)");
            break;
        case Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen:
            _linker.NavBarInstance.SetFullscreenTooltip("退出全屏 (F11)");
            _linker.NavBarInstance.SetSidebarTooltip("停靠到边栏 (Ctrl+Shift+S)");
            break;
        case Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar:
            _linker.NavBarInstance.SetFullscreenTooltip("进入全屏 (F11)");
            _linker.NavBarInstance.SetSidebarTooltip("取消停靠 (Ctrl+Shift+S)");
            break;
    }
}
```

## 任务 13.8: 添加转换通知消息

### 需求
- 当用户在进行中的转换期间尝试触发转换时显示简短通知
- 使用非侵入式 UI（例如 toast 通知）

### 实现步骤

```csharp
public void ShowTransitionInProgressNotification()
{
    // 使用 WinUI3 的 InfoBar 或 TeachingTip 显示通知
    // 或者使用 Windows 通知系统
    
    // 示例：使用 Debug 输出（实际应该使用 UI 通知）
    System.Diagnostics.Debug.WriteLine("窗口正在切换模式，请稍候...");
    
    // TODO: 实现实际的 UI 通知
    // 可以在 Linker 中添加一个 InfoBar 控件
    // _linker?.ShowTransitionNotification("窗口正在切换模式，请稍候...");
}
```

在 `ToggleFullscreen`、`ToggleSidebar` 等方法中添加检查：

```csharp
public void ToggleFullscreen()
{
    if (_stateManager == null)
    {
        _windowController.ToggleMaximize();
        return;
    }
    
    // 检查是否正在转换
    if (_stateManager.TransitioningTo != null)
    {
        ShowTransitionInProgressNotification();
        return;
    }
    
    // 执行状态转换
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen)
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen);
    }
}
```

## 完整的 MainWindow.xaml.cs 更新示例

以下是集成所有 UI 更新后的完整示例：

```csharp
// 在 MainWindow 类中添加字段
private Docked_AI.功能.主窗口v2.状态机.WindowState _lastVisibleState = 
    Docked_AI.功能.主窗口v2.状态机.WindowState.Floating;

// 更新事件处理器
private void OnWindowStateChanged(
    Docked_AI.功能.主窗口v2.状态机.WindowState from, 
    Docked_AI.功能.主窗口v2.状态机.WindowState to)
{
    System.Diagnostics.Debug.WriteLine($"MainWindow: State changed from {from} to {to}");
    
    // 保存上次可见状态
    if (to != Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden &&
        to != Docked_AI.功能.主窗口v2.状态机.WindowState.Initializing)
    {
        _lastVisibleState = to;
    }
    
    // 启用按钮
    if (_linker != null)
    {
        _linker.NavBarInstance.SetButtonsEnabled(true);
    }
    
    // 更新按钮视觉状态
    UpdateButtonStates(to);
    
    // 更新工具提示
    UpdateTooltips(to);
}

private void OnWindowTransitionStarted(
    Docked_AI.功能.主窗口v2.状态机.WindowState from, 
    Docked_AI.功能.主窗口v2.状态机.WindowState to)
{
    System.Diagnostics.Debug.WriteLine($"MainWindow: Transition started from {from} to {to}");
    
    // 禁用相关按钮，防止重复点击
    if (_linker != null)
    {
        _linker.NavBarInstance.SetButtonsEnabled(false);
    }
}

// 添加新的公共方法
public void ToggleFullscreen()
{
    if (_stateManager == null)
    {
        _windowController.ToggleMaximize();
        return;
    }
    
    if (_stateManager.TransitioningTo != null)
    {
        ShowTransitionInProgressNotification();
        return;
    }
    
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen)
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen);
    }
}

public void ToggleSidebar()
{
    if (_stateManager == null)
    {
        TogglePinnedDock();
        return;
    }
    
    if (_stateManager.TransitioningTo != null)
    {
        ShowTransitionInProgressNotification();
        return;
    }
    
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar)
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar);
    }
}

public void ToggleWindowVisibility()
{
    if (_stateManager == null)
    {
        ToggleWindow();
        return;
    }
    
    var currentState = _stateManager.CurrentState;
    if (currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden)
    {
        _stateManager.TransitionTo(_lastVisibleState);
    }
    else
    {
        _stateManager.TransitionTo(Docked_AI.功能.主窗口v2.状态机.WindowState.Hidden);
    }
}

private void UpdateButtonStates(Docked_AI.功能.主窗口v2.状态机.WindowState currentState)
{
    if (_linker == null) return;
    
    bool isFullscreen = currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Fullscreen;
    _linker.NavBarInstance.UpdateWindowStateIcon(isFullscreen);
    
    bool isSidebar = currentState == Docked_AI.功能.主窗口v2.状态机.WindowState.Sidebar;
    _linker.NavBarInstance.UpdateDockToggleIcon(isSidebar);
    
    _linker.UpdateContentCornerRadius(isSidebar);
    _linker.UpdateContentTopMargin(isFullscreen || isSidebar);
}

private void UpdateTooltips(Docked_AI.功能.主窗口v2.状态机.WindowState currentState)
{
    if (_linker == null) return;
    
    if (_stateManager?.TransitioningTo != null)
    {
        // 显示转换中的提示
        return;
    }
    
    // 根据当前状态更新工具提示
    // TODO: 实现具体的工具提示更新逻辑
}

private void ShowTransitionInProgressNotification()
{
    System.Diagnostics.Debug.WriteLine("窗口正在切换模式，请稍候...");
    // TODO: 实现实际的 UI 通知
}
```

## 下一步

完成 UI 集成后，继续执行：
- 任务 14: 实现生命周期钩子行为
- 任务 15: 添加日志和遥测
- 任务 16: 最终集成和测试
- 任务 17: 文档和清理
- 任务 18: 最终检查点

## 注意事项

1. **向后兼容性**: 所有新方法都包含了对 `_stateManager == null` 的检查，确保在新架构初始化失败时回退到旧架构
2. **线程安全**: WindowStateManager 内部已经处理了线程安全问题，可以从任意线程调用 TransitionTo
3. **事件订阅**: 确保在窗口关闭时取消订阅所有事件，避免内存泄漏
4. **测试**: 在实现每个功能后，手动测试确保功能正常工作

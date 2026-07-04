# Bugfix Requirements Document

## Introduction

本文档描述了"应用静默退出问题"的修复需求。应用在长时间空闲后会静默退出,没有明确的错误提示,导致用户体验不佳。Windows 事件日志显示与 `Microsoft.UI.Xaml.dll` 相关的异常代码 `0xc000027b`,表明这是一个 XAML 未处理异常导致的崩溃问题。

经过代码审查,已识别出五个高优先级崩溃隐患点:
1. **Application.UnhandledException 只记录,不设置 handled** - 导致 XAML 异常直接退出应用
2. **大量 async void 方法** - 空闲后恢复时异常路径处理不当
3. **WebView2 缺少进程失败事件处理** - 浏览器进程崩溃时无法恢复
4. **托盘隐藏窗口/keep-alive 窗口生命周期脆弱** - 窗口意外关闭导致进程退出
5. **托盘 SystemTrayIcon 的 subclass 生命周期问题** - 原生回调访问已释放对象导致崩溃

**信息来源:**
- [WebView2 进程相关事件](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events)
- [Windows App SDK 应用生命周期](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle)

## Bug Analysis

### 1. Current Behavior (Defect)

**1.1 Application.UnhandledException 未设置 Handled**

1.1.1 WHEN XAML UI 线程在空闲后收到 Dispatcher 回调异常 THEN 系统记录异常但立即退出应用,没有给用户任何提示

1.1.2 WHEN WebView2 导航完成回调抛出异常 THEN 系统记录异常但进程静默终止

1.1.3 WHEN 窗口状态变化事件处理器抛出异常 THEN 系统记录异常但应用崩溃退出

**1.2 async void 方法异常路径处理**

1.2.1 WHEN `OnWindowStateChanged` 方法在 await 后抛出异常 THEN 异常走 XAML UnhandledException 路径,应用直接退出

1.2.2 WHEN `CoreWebView2_NavigationCompleted` 方法在处理导航完成逻辑时抛出异常 THEN 异常无法被调用方捕获,导致应用崩溃

1.2.3 WHEN 长时间空闲后恢复,async void 方法中的 UI 对象已释放 THEN 访问已释放对象导致应用崩溃

**1.3 WebView2 进程失败处理缺失**

1.3.1 WHEN WebView2 浏览器进程意外退出 THEN 系统没有监听 `ProcessFailed` 事件,无法记录崩溃原因

1.3.2 WHEN WebView2 渲染进程崩溃 THEN 系统没有恢复机制,WebView 显示空白或冻结

1.3.3 WHEN WebView2 主浏览器进程退出 THEN 系统没有重建 WebView2 控件,导致功能完全失效

**1.4 Keep-alive 窗口生命周期管理**

1.4.1 WHEN 某处代码意外关闭了主窗口和 keep-alive 窗口 THEN 进程立即静默退出,没有任何提示

1.4.2 WHEN 托盘模式下主窗口关闭 THEN 依赖 `_keepAliveWindow` 保持进程,但如果该窗口被意外释放则应用退出

1.4.3 WHEN 最后一个窗口关闭 THEN Windows App SDK 桌面应用会终止进程(不像 UWP 有 suspend/resume)

**1.5 SystemTrayIcon 原生回调生命周期**

1.5.1 WHEN 托盘图标使用 `GCHandleType.Weak` 传给 `SetWindowSubclass` THEN 对象被 GC 回收后原生回调访问弱引用导致崩溃

1.5.2 WHEN `SystemTrayIcon.Dispose()` 被调用 THEN 只关闭隐藏窗口,没有调用 `RemoveWindowSubclass`,导致悬挂的原生回调

1.5.3 WHEN 原生窗口回调触发但托管对象已释放 THEN 访问无效内存导致进程崩溃,没有 .NET 异常堆栈

### 2. Expected Behavior (Correct)

**2.1 Application.UnhandledException 正确处理**

2.1.1 WHEN XAML UI 线程抛出未处理异常 THEN 系统 SHALL 设置 `e.Handled = true`,记录完整异常上下文(包括堆栈、时间、应用状态),并阻止应用退出

2.1.2 WHEN 异常发生在关键组件(如主窗口、WebView) THEN 系统 SHALL 记录额外的诊断信息(窗口状态、WebView URL、Dispatcher 队列状态)

2.1.3 WHEN 捕获 XAML 异常后 THEN 系统 SHALL 在 UI 线程上继续运行,保持应用稳定性

**2.2 async void 方法改造**

2.2.1 WHEN 需要异步事件处理器时 THEN 系统 SHALL 使用 `async Task` 包装器 + `_ = FireAndForget(handler)` 模式

2.2.2 WHEN `FireAndForget` 包装器捕获异常 THEN 系统 SHALL 记录异常并优雅降级,而不是让应用崩溃

2.2.3 WHEN 长时间空闲后恢复触发事件 THEN 系统 SHALL 在 async 方法中检查对象生命周期,避免访问已释放对象

**2.3 WebView2 进程失败恢复**

2.3.1 WHEN WebView2 初始化成功后 THEN 系统 SHALL 订阅 `CoreWebView2.ProcessFailed` 事件

2.3.2 WHEN `ProcessFailed` 事件触发 THEN 系统 SHALL 记录失败类型(`e.ProcessFailedKind`)、原因(`e.Reason`)、进程描述信息

2.3.3 WHEN 主浏览器进程退出(`BrowserProcessExited`) THEN 系统 SHALL 自动重建 WebView2 控件或提示用户重新加载

**2.4 Keep-alive 窗口生命周期保护**

2.4.1 WHEN 应用启动进入托盘模式 THEN 系统 SHALL 创建并保持 `_keepAliveWindow` 的强引用

2.4.2 WHEN 主窗口关闭事件触发 THEN 系统 SHALL 检查 `_keepAliveWindow` 是否仍然存在,如果不存在则重新创建

2.4.3 WHEN 检测到 keep-alive 窗口意外关闭 THEN 系统 SHALL 立即重建窗口并记录警告日志

**2.5 SystemTrayIcon 原生回调管理**

2.5.1 WHEN 创建 SystemTrayIcon 时 THEN 系统 SHALL 使用 `GCHandleType.Normal`(强引用)代替 `GCHandleType.Weak`

2.5.2 WHEN `SystemTrayIcon.Dispose()` 被调用 THEN 系统 SHALL 先调用 `RemoveWindowSubclass` 移除原生回调,再关闭窗口,最后释放 GCHandle

2.5.3 WHEN 原生窗口回调触发 THEN 系统 SHALL 确保托管对象仍然有效,通过强引用保证对象生命周期

### 3. Unchanged Behavior (Regression Prevention)

**3.1 正常启动和关闭流程**

3.1.1 WHEN 用户正常启动应用 THEN 系统 SHALL CONTINUE TO 正常显示主窗口和初始化所有组件

3.1.2 WHEN 用户主动退出应用 THEN 系统 SHALL CONTINUE TO 执行清理逻辑并正常退出

3.1.3 WHEN 用户最小化到托盘 THEN 系统 SHALL CONTINUE TO 隐藏主窗口并保持后台运行

**3.2 WebView2 正常导航和功能**

3.2.1 WHEN WebView2 正常加载网页 THEN 系统 SHALL CONTINUE TO 显示网页内容并响应用户交互

3.2.2 WHEN 用户在 WebView2 中导航 THEN 系统 SHALL CONTINUE TO 更新导航按钮状态和应用主题色

3.2.3 WHEN WebView2 处理 JavaScript 回调 THEN 系统 SHALL CONTINUE TO 正确执行消息传递和事件处理

**3.3 窗口状态管理**

3.3.1 WHEN 用户通过快捷键或托盘图标切换窗口可见性 THEN 系统 SHALL CONTINUE TO 执行显示/隐藏动画和状态转换

3.3.2 WHEN 窗口最大化/还原/最小化 THEN 系统 SHALL CONTINUE TO 触发 `WindowMaximizedMonitorService` 监听并更新状态

3.3.3 WHEN 用户拖动窗口或调整大小 THEN 系统 SHALL CONTINUE TO 响应并保存窗口位置和尺寸

**3.4 托盘图标功能**

3.4.1 WHEN 用户右键点击托盘图标 THEN 系统 SHALL CONTINUE TO 显示上下文菜单

3.4.2 WHEN 用户左键点击托盘图标 THEN 系统 SHALL CONTINUE TO 切换主窗口显示/隐藏

3.4.3 WHEN 系统主题切换(明暗模式) THEN 系统 SHALL CONTINUE TO 更新托盘图标样式

**3.5 全局系统钩子和监听服务**

3.5.1 WHEN `WindowMaximizedMonitorService` 监听其他窗口最大化事件 THEN 系统 SHALL CONTINUE TO 正常触发回调并执行业务逻辑

3.5.2 WHEN 全局 WinEvent Hook 接收系统窗口事件 THEN 系统 SHALL CONTINUE TO 在正确的线程上处理事件

3.5.3 WHEN 应用空闲时后台 Timer 触发 THEN 系统 SHALL CONTINUE TO 执行定时任务但不影响应用稳定性

---

## Bug Condition Derivation

### Bug Condition Function

```pascal
FUNCTION isBugCondition(X)
  INPUT: X of type ApplicationState
  OUTPUT: boolean
  
  // 返回 true 当以下任一条件满足:
  // 1. XAML 未处理异常发生但未设置 Handled
  // 2. async void 方法在 await 后抛出异常
  // 3. WebView2 进程失败但未订阅 ProcessFailed 事件
  // 4. keep-alive 窗口意外关闭
  // 5. SystemTrayIcon 原生回调访问已释放对象
  
  RETURN (X.hasXamlException AND NOT X.isExceptionHandled)
      OR (X.hasAsyncVoidException)
      OR (X.webView2ProcessFailed AND NOT X.hasProcessFailedHandler)
      OR (X.keepAliveWindowClosed AND X.isInTrayMode)
      OR (X.trayIconCallbackInvalid)
END FUNCTION
```

### Property Specification

```pascal
// Property: Fix Checking - 应用不再静默退出
FOR ALL X WHERE isBugCondition(X) DO
  result ← HandleApplicationState'(X)
  
  ASSERT result.applicationRunning = true
    AND result.exceptionLogged = true
    AND result.hasUserNotification = true  // 严重错误时通知用户
    AND result.componentRecovered = true   // 尝试恢复受影响组件
END FOR
```

**Key Definitions:**
- **F**: 原始未修复的应用代码 - 遇到上述条件时会静默退出
- **F'**: 修复后的应用代码 - 捕获异常、记录日志、恢复组件、保持运行

### Preservation Goal

```pascal
// Property: Preservation Checking - 正常功能不受影响
FOR ALL X WHERE NOT isBugCondition(X) DO
  ASSERT F(X) = F'(X)
  // 即: 修复不应改变任何正常工作流的行为
END FOR
```

这确保了修复只针对 bug 场景,不会引入回归问题。

---

## 修复优先级

基于影响范围和修复复杂度,建议按以下顺序修复:

1. **P0 - Application.UnhandledException 设置 Handled** (止血措施)
   - 影响: 所有 XAML 未处理异常
   - 复杂度: 低(一行代码)
   - 预期效果: 立即阻止大部分静默退出

2. **P1 - WebView2 ProcessFailed 事件订阅**
   - 影响: WebView2 相关崩溃
   - 复杂度: 中(需要实现恢复逻辑)
   - 预期效果: 浏览器进程崩溃后可恢复

3. **P1 - SystemTrayIcon 生命周期修复**
   - 影响: 托盘图标相关原生崩溃
   - 复杂度: 中(需要修改 GCHandle 和 subclass 清理)
   - 预期效果: 消除原生回调相关崩溃

4. **P2 - async void 方法重构**
   - 影响: 所有异步事件处理器
   - 复杂度: 高(需要重构多个方法)
   - 预期效果: 异步异常路径更可控

5. **P2 - Keep-alive 窗口生命周期保护**
   - 影响: 托盘模式稳定性
   - 复杂度: 中(需要添加监控和自动恢复)
   - 预期效果: 防止窗口意外关闭导致退出

---

## Counterexamples (具体复现场景)

**Counterexample 1: XAML 未处理异常**
```
输入: 应用空闲 2 小时后,WebView2 导航完成回调访问已释放的 UI 元素
当前行为: 抛出 System.NullReferenceException,应用静默退出
预期行为: 异常被捕获并记录,WebView 显示错误提示,应用继续运行
```

**Counterexample 2: WebView2 进程崩溃**
```
输入: WebView2 主浏览器进程因内存不足被系统终止
当前行为: WebView 显示空白,没有日志记录,用户无法恢复
预期行为: 记录 ProcessFailed 事件,显示"浏览器进程已崩溃,点击重新加载"按钮
```

**Counterexample 3: 托盘 subclass 悬挂指针**
```
输入: SystemTrayIcon 对象被 GC 回收,但原生 subclass 回调仍在注册
当前行为: 原生回调触发时访问无效内存,进程崩溃(无 .NET 堆栈)
预期行为: Dispose 时正确移除 subclass,使用强引用防止提前 GC
```

嗷呜~ 🐺
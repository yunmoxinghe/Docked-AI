# 动画打断功能测试

## 概述

本目录包含用于验证窗口状态管理器核心功能的测试：**动画打断和请求压缩**。

重构的核心目的是实现平滑的动画打断，允许用户快速连续切换窗口状态而不会出现卡顿或视觉跳变。

## 核心功能

### 1. 动画打断（Animation Interruption）

当用户快速连续触发多个状态转换时，系统应该：
- 立即取消当前正在执行的动画
- 从当前中间视觉状态继续插值到新目标
- 不执行反向动画（不回到起始状态）
- 保持视觉连续性，无跳变

### 2. 请求压缩（Request Compression）

当多个状态转换请求快速连续发出时，系统应该：
- 只执行到最后一个目标状态的转换
- 不排队中间转换
- 中间状态不会成为稳定状态
- StateChanged 事件只触发一次，显示从起始稳定状态到最终稳定状态

## 测试文件

### 动画打断测试.cs

理论测试，描述测试场景和预期结果。

### 动画打断集成测试.cs

实际集成测试，运行状态管理器并验证功能。

包含两个测试：
1. **测试 1：快速连续状态切换** - 验证请求压缩
2. **测试 2：状态一致性** - 验证打断后的状态正确性

## 如何运行测试

### 方法 1：通过主窗口运行

在主窗口显示后，通过调试器或代码调用：

```csharp
await mainWindow.RunAnimationInterruptionTests();
```

### 方法 2：手动测试

1. 启动应用
2. 快速连续点击不同的窗口状态按钮（浮窗、全屏、边栏）
3. 观察窗口是否平滑过渡，无跳变
4. 检查调试输出中的状态转换日志

### 方法 3：通过托盘图标测试

1. 启动应用（窗口隐藏）
2. 右键托盘图标，选择"运行动画打断测试"（如果已添加菜单项）
3. 查看调试输出

## 预期结果

### 测试 1：快速连续状态切换

**操作：**
```
TransitionTo(Floating)
TransitionTo(Fullscreen)  // 立即打断
TransitionTo(Sidebar)     // 立即打断
```

**预期：**
- StateChanged 只触发 1 次：`Initializing → Sidebar`
- TransitionStarted 可能触发多次（取决于打断时机）
- 最终状态是 `Sidebar`
- 中间状态（Floating、Fullscreen）不会成为稳定状态

### 测试 2：状态一致性

**操作：**
```
TransitionTo(Floating)
等待 50ms
TransitionTo(Fullscreen)  // 打断
等待转换完成
```

**预期：**
- CurrentState 最终是 `Fullscreen`
- TransitioningTo 最终是 `null`
- 动画从中间状态平滑过渡到 Fullscreen

## 诊断问题

### 问题 1：动画无法打断

**症状：**
- 快速点击按钮时，窗口仍然执行所有中间状态的动画
- StateChanged 触发多次

**可能原因：**
1. CancellationToken 未正确传递到动画引擎
2. Task.Delay 未使用 CancellationToken
3. 状态循环未检查 _latestTarget

**检查点：**
- `AnimationEngine.Animate` 是否接受并使用 `cancellationToken`
- `Task.Delay(16, cancellationToken)` 是否正确传递令牌
- `RunStateMachineLoop` 中的 `while (_latestTarget != CurrentState)` 是否正确

### 问题 2：动画打断后出现视觉跳变

**症状：**
- 打断后窗口突然跳到新位置，而不是平滑过渡

**可能原因：**
1. 打断后执行了反向动画
2. _currentVisual 未正确保持在中间状态
3. 新动画从错误的起始状态开始

**检查点：**
- `OperationCanceledException` 捕获后，`_currentVisual` 是否保持不变
- 新动画是否从 `_currentVisual` 开始插值
- `onProgress` 回调是否正确更新 `_currentVisual`

### 问题 3：取消令牌响应慢

**症状：**
- TransitionTo 调用后，旧动画仍然播放一段时间才停止

**可能原因：**
1. Task.Delay 时间过长（应该是 ~16ms）
2. 未在循环开始时检查 cancellationToken
3. Interlocked.Exchange 未正确替换 CTS

**检查点：**
- `Task.Delay` 的延迟是否为 16ms
- 循环开始时是否调用 `cancellationToken.ThrowIfCancellationRequested()`
- `TransitionTo` 中是否使用 `Interlocked.Exchange` 原子替换

## 调试输出示例

### 正常工作的输出

```
[StateManager] Transition started: Initializing → Floating
[StateManager] Transition cancelled: Initializing → Floating
[StateManager] Note: OnExit was not called for state Initializing
[StateManager] Transition started: Initializing → Fullscreen
[StateManager] Transition cancelled: Initializing → Fullscreen
[StateManager] Note: OnExit was not called for state Initializing
[StateManager] Transition started: Initializing → Sidebar
[StateManager] Transition completed: Initializing → Sidebar
[Test] StateChanged #1: Initializing → Sidebar
✅ 测试 1 通过
```

### 异常输出（动画未打断）

```
[StateManager] Transition started: Initializing → Floating
[StateManager] Transition completed: Initializing → Floating
[Test] StateChanged #1: Initializing → Floating
[StateManager] Transition started: Floating → Fullscreen
[StateManager] Transition completed: Floating → Fullscreen
[Test] StateChanged #2: Floating → Fullscreen
[StateManager] Transition started: Fullscreen → Sidebar
[StateManager] Transition completed: Fullscreen → Sidebar
[Test] StateChanged #3: Fullscreen → Sidebar
❌ 失败：StateChanged 应该只触发 1 次，实际触发 3 次
```

## 相关需求

- **需求 2.2**: 多个 TransitionTo 请求快速连续发出时，只执行到最后一个目标状态的转换
- **需求 2.3**: 不排队中间转换，始终收敛到最新的目标状态
- **需求 2.5**: 新的 TransitionTo 请求到达时，取消当前正在执行的动画
- **需求 3.2**: 动画被打断时，从当前中间视觉状态继续插值到新目标状态
- **需求 3.3**: 动画被打断时，不执行反向动画来恢复到起始状态
- **需求 3.8**: 支持通过 CancellationToken 立即停止动画
- **需求 16.1**: 动画从中间视觉状态继续，无视觉跳跃

## 总结

动画打断是本次重构的核心功能，确保用户体验流畅自然。如果测试失败，说明核心架构存在问题，需要立即修复。

关键设计点：
1. **单线程状态循环** - 避免并发问题
2. **CancellationToken** - 实现即时打断
3. **_currentVisual 保持中间状态** - 确保视觉连续性
4. **请求压缩** - 只执行最后一个目标

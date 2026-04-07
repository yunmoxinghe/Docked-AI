# Docked Tools 窗口管理架构设计文档

v0.1 Draft·主窗口重构

| 文档状态 | 架构版本 | 适用模块 |
|---------|---------|---------|
| 草稿 Draft | v0.1 | 主窗口/窗口管理 |

## 一、概述

当前主窗口模块代码耦合度高、职责划分不清晰，导致维护困难。本次重构目标是：

- 解耦服务层​ – 将标题栏、背景、Win32抽象从主窗口代码中剥离
- 建立清晰的状态机​ – 用枚举+异步转换替代散落的条件分支
- 动画模块化​ – 每个窗口形态自行管理进入、退出、打断动画
- 提高可测试性​ – 通过接口隔离，各层可独立单测

重构后采用三层架构，严格分离关注点：

| 服务层 WindowService | 状态机 WindowStateManager | 表现层 各窗口形态 |
|---------------------|-------------------------|-----------------|
| 无状态函数集<br>翻译 Win32 API | 持有状态+校验<br>异步转换+事件广播 | FloatingWindow<br>FullscreenWindow<br>SidebarWindow |

## 二、枚举定义

### 2.1 WindowState (窗口状态)

描述窗口当前所处的状态。任意时刻只有一个状态有效（互斥）。

```csharp
enum WindowState {
    Initializing, // 首次创建，WinUI3内部行为，不受状态机控制
    Hidden, // 隐藏（窗口存在但不可见）
    Floating, // 浮窗模式
    Fullscreen, // 全屏模式
    Sidebar // 边栏模式
}
```

**Initializing**: 唯一起点，只能从此状态出发，不可转入

### 2.2 合法状态转换规则

| 起始状态 | 可转换到 | 说明 |
|---------|---------|------|
| Initializing | Floating / Fullscreen / Sidebar | Activate() 后窗口已显示，应转换到可见状态 |
| Initializing | Hidden | 特殊情况：如需启动时隐藏，需在 Activate() 前设置 Opacity=0 |
| Hidden | Floating / Fullscreen / Sidebar | 任意目标均可 |
| Floating | Hidden / Fullscreen / Sidebar | 任意目标均可 |
| Fullscreen | Hidden / Floating / Sidebar | 任意目标均可 |
| Sidebar | Hidden / Floating / Fullscreen | 任意目标均可 |

**重要原则：**
- Initializing 是唯一起点，只能从此状态出发，不可转入
- Activate() 后窗口已显示，通常应转换到可见状态（Floating/Fullscreen/Sidebar）
- 只有在特殊需求下（如托盘应用）才从 Initializing 转换到 Hidden

## 三、统一视觉空间与声明式动画

**核心设计理念：状态定义"目标样子"，动画系统统一插值**

不同于传统的"命令式动画"（OnEnter/OnExit），本架构采用"声明式动画"模型：
- 状态只负责定义"最终视觉效果"和"动画偏好"
- 动画系统负责从"当前视觉状态"平滑过渡到"目标视觉状态"
- 打断时无需反向动画，直接从当前视觉状态插值到新目标

### 3.1 WindowVisualState（统一视觉空间）

所有窗口属性的快照，定义窗口在某一时刻的完整视觉状态。

```csharp
/// <summary>
/// 窗口视觉状态快照，定义窗口的完整外观
/// </summary>
class WindowVisualState
{
    public Rect Bounds { get; set; }           // 窗口位置和尺寸
    public double CornerRadius { get; set; }   // 圆角半径
    public double Opacity { get; set; }        // 不透明度（0.0 - 1.0）
    public bool IsTopmost { get; set; }        // 是否置顶
    public int ExtendedStyle { get; set; }     // Win32 扩展样式
    // 根据需要扩展其他视觉属性
}
```

**关键特性：**
- 所有可动画属性都在此定义（Bounds、CornerRadius、Opacity 等连续量）
- 支持线性插值（LERP）或 Spring 插值
- 状态转换时，动画系统自动计算中间帧

**属性分类：**

| 属性类型 | 是否可插值 | 示例 | 说明 |
|---------|-----------|------|------|
| 连续量 | ✅ | Bounds, CornerRadius, Opacity | 可以平滑过渡 |
| 离散状态 | ❌ | IsVisible, IsHitTestVisible | 只能切换，不能插值 |

**重要设计原则：**
- IsVisible（窗口是否存在于视觉树）不属于 WindowVisualState，由状态机生命周期钩子控制
- 渐显/渐隐效果通过 Opacity 实现，而非 IsVisible 的硬切换
- 这样可以避免"动画中途突然显示/隐藏"的跳变感

### 3.2 IWindowState接口（声明式）

FloatingWindow、FullscreenWindow、SidebarWindow各自实现此接口，定义目标视觉和动画偏好。

```csharp
/// <summary>
/// 窗口形态接口，每个形态定义目标视觉和动画规格
/// </summary>
interface IWindowState
{
    /// <summary>
    /// 获取该状态的目标视觉效果
    /// </summary>
    WindowVisualState GetTargetVisual();

    /// <summary>
    /// 获取动画规格，可根据起点和终点动态调整
    /// </summary>
    /// <param name="from">当前视觉状态</param>
    /// <param name="to">目标视觉状态（通常是 GetTargetVisual() 的返回值）</param>
    AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to);

    /// <summary>
    /// 进入该状态时的生命周期钩子（在动画开始前调用）
    /// </summary>
    void OnEnter();

    /// <summary>
    /// 离开该状态时的生命周期钩子（在动画完成后调用）
    /// </summary>
    void OnExit();
}
```

**生命周期钩子用途：**
- OnEnter: 控制离散状态（如设置 IsVisible = true, IsHitTestVisible = true）
- OnExit: 清理资源或设置离散状态（如设置 IsVisible = false）
- 这些钩子不参与动画插值，只在状态切换的关键时刻执行

### 3.3 AnimationSpec（动画规格）

定义动画的时长、缓动函数、插值策略等。

```csharp
/// <summary>
/// 动画规格，定义如何从当前状态过渡到目标状态
/// </summary>
class AnimationSpec
{
    public TimeSpan Duration { get; set; }
    public Easing Easing { get; set; }  // 缓动函数（如 EaseInOut）

    // 可选：针对不同属性使用不同缓动
    public Func<double, double>? BoundsEasing { get; set; }
    public Func<double, double>? CornerRadiusEasing { get; set; }

    // 可选：使用 Spring 物理模拟
    public SpringConfig? Spring { get; set; }
}

/// <summary>
/// Spring 物理模拟配置
/// </summary>
class SpringConfig
{
    public double Stiffness { get; set; }  // 刚度
    public double Damping { get; set; }    // 阻尼
}
```

**设计优势：**
- **上下文感知**：`GetAnimationSpec(from, to)` 可根据起点和终点动态调整动画参数
  - 例如：如果 `from` 很接近 `to`，可以缩短 `Duration`
  - 例如：如果距离很远，可以使用 Spring 插值使动画更自然
- **全局一致性**：动画系统统一执行插值，易于调试和优化
- **可扩展性**：可针对不同属性使用不同缓动函数

## 四、WindowStateManager (状态机)

持有当前状态，负责校验合法性、驱动动画系统、广播状态变更事件。不直接操作UI。

**核心设计理念：声明式状态机 + 统一动画调度器**

- 不是传统的"并发控制"（加锁），而是"请求压缩" - 只保留最后一个目标
- TransitionTo 不执行动画，只更新 _latestTarget
- 使用单线程状态循环 RunStateMachineLoop() 驱动所有转换
- 状态机永远在"追逐"_latestTarget，通过 CancellationTokenSource 实现打断
- 动画系统从"当前视觉状态"插值到"目标视觉状态"，无需反向动画
- 循环条件：`while (_latestTarget != CurrentState)` - 语义清晰，无隐式状态

```csharp
class WindowStateManager
{
    // - 状态属性 -
    // 当前稳定状态（只读）
    public WindowState CurrentState { get; private set; }
    
    // 上一个稳定状态（用于动画参数计算）
    private WindowState _lastStableState;
    
    // 正在转换到的目标状态（只读，null 表示没有正在进行的转换）
    public WindowState? TransitioningTo { get; private set; }
    
    // 当前窗口的实际视觉状态（实时更新）
    private WindowVisualState _currentVisual;
    
    // - 并发控制字段 -
    // 最新的目标状态（状态机永远在"追逐"这个目标）
    private WindowState _latestTarget;
    // 状态机循环是否正在运行
    private bool _isRunning;
    // 当前动画的取消令牌源（用于打断正在执行的动画）
    private CancellationTokenSource? _currentCts;
    // UI 线程调度器（用于线程安全）
    private readonly DispatcherQueue _dispatcher;
    
    // 动画系统（统一插值引擎）
    private readonly AnimationEngine _animationEngine;
    
    // 全局动画策略（可选，用于统一管理动画参数）
    private readonly IAnimationPolicy? _animationPolicy;

    // - 事件 -
    // 状态切换完成后广播（含所有动画已结束）
    // 参数：from=切换前状态，to=切换后状态
    public event Action<WindowState, WindowState>? StateChanged;
    
    // 开始转换到新状态时广播
    // 参数：from=当前状态，to=目标状态
    public event Action<WindowState, WindowState>? TransitionStarted;

    // - 方法 -
    // 请求切换到目标状态（立即返回，不等待动画完成）
    // 线程安全：可从任意线程调用，内部自动转发到 UI 线程
    // 更新 _latestTarget 并立即取消当前动画（如果有）
    // 如果状态机未运行，则启动 RunStateMachineLoop()
    public void TransitionTo(WindowState target) { ... }
    
    // 状态机主循环（私有，由 TransitionTo 触发）
    // 持续执行直到 _latestTarget 与 CurrentState 一致
    private async Task RunStateMachineLoop() { ... }
}
```

### 4.1 TransitionTo 方法

**TransitionTo 的职责：更新目标并原子替换 CTS**

```csharp
public void TransitionTo(WindowState target) {
    // 🔒 线程安全：强制在 UI 线程执行
    if (!_dispatcher.HasThreadAccess) {
        _dispatcher.TryEnqueue(() => TransitionTo(target));
        return;
    }
    
    _latestTarget = target;
    
    // 🎯 原子替换并取消旧的 CTS（避免对已 Dispose 的 CTS 调用 Cancel）
    var oldCts = Interlocked.Exchange(ref _currentCts, new CancellationTokenSource());
    oldCts?.Cancel();
    oldCts?.Dispose();
    
    if (!_isRunning)
        _ = RunStateMachineLoop();
}
```

**关键点：**

- **线程安全**：可从任意线程调用，内部自动通过 `DispatcherQueue` 转发到 UI 线程
- TransitionTo 立即返回，不等待动画完成
- 更新 _latestTarget 字段，表示"用户想去哪里"
- **原子替换**：使用 `Interlocked.Exchange` 原子地替换 `_currentCts`，避免竞态条件
- **安全取消**：先替换再取消旧的 CTS，确保不会对已 Dispose 的对象调用 Cancel
- 如果状态机循环未运行，则启动它

### 4.2 RunStateMachineLoop 状态机主循环

所有状态转换由单线程循环驱动，使用 `CancellationTokenSource` 实现即时打断通知。采用"声明式动画"模式，从当前视觉状态平滑插值到目标视觉状态。

```csharp
private async Task RunStateMachineLoop() {
    _isRunning = true;
    
    while (_latestTarget != CurrentState) {
        var target = _latestTarget;
        var from = CurrentState;
        
        // 🎯 设置过渡状态并广播
        TransitioningTo = target;
        TransitionStarted?.Invoke(from, target);
        
        var targetImpl = GetImpl(target);
        
        // 0️⃣ 调用目标状态的 OnEnter 钩子（设置离散状态）
        targetImpl.OnEnter();
        
        // 1️⃣ 获取目标视觉状态和动画规格
        var targetVisual = targetImpl.GetTargetVisual();
        
        // 使用稳定状态计算动画参数（而非中间状态）
        var animationSpec = _animationPolicy?.Resolve(_lastStableState, target, _currentVisual)
            ?? targetImpl.GetAnimationSpec(_currentVisual, targetVisual);
        
        // 2️⃣ 执行动画（从当前视觉状态插值到目标视觉状态）
        var animationCts = _currentCts; // 快照当前 CTS
        
        try {
            // 动画系统统一执行插值，实时更新 _currentVisual
            await _animationEngine.Animate(
                from: _currentVisual,
                to: targetVisual,
                spec: animationSpec,
                onProgress: (visual) => {
                    _currentVisual = visual;
                    ApplyVisualToWindow(visual); // 应用到实际窗口
                },
                cancellationToken: animationCts.Token
            );
        } catch (OperationCanceledException) {
            // 动画被打断，_currentVisual 已停留在中间状态
            // 下一轮循环会从这个中间状态继续插值到新目标
            TransitioningTo = null;
            continue;
        } finally {
            // 只释放"自己这一轮"的 CTS（防止误释放新一轮的 CTS）
            if (animationCts == _currentCts) {
                animationCts.Dispose();
                _currentCts = null;
            }
        }
        
        // 3️⃣ 动画完成，调用旧状态的 OnExit 钩子
        var fromImpl = GetImpl(from);
        fromImpl.OnExit();
        
        // 4️⃣ 更新状态
        _lastStableState = from;
        CurrentState = target;
        TransitioningTo = null;
        StateChanged?.Invoke(from, target);
    }
    
    _isRunning = false;
}
```

**关键设计点：**

- **无需 OnExit/OnEnter 分段**：直接从当前视觉状态插值到目标视觉状态
- **无需反向动画**：打断时 `_currentVisual` 已停留在中间状态，下一轮直接从这里继续
- **统一插值引擎**：`AnimationEngine.Animate()` 负责所有插值逻辑，支持 LERP、Spring 等
- **实时进度更新**：`onProgress` 回调实时更新 `_currentVisual` 和窗口外观
- **资源安全**：快照 + 条件释放模式，防止误释放或重复释放 CTS
- **稳定 from 状态**：使用 `_lastStableState` 而非中间状态计算动画参数，确保动画手感一致
- **生命周期钩子**：OnEnter 在动画前调用，OnExit 在动画后调用，用于控制离散状态

### 4.3 执行流程示例

**场景：用户快速切换 Hidden → Floating → Fullscreen → Sidebar**

| 时间线 | 用户操作 | _latestTarget | 循环执行 | CurrentState | _currentVisual | TransitioningTo |
|-------|---------|--------------|---------|-------------|----------------|-----------------|
| T0 | TransitionTo(Floating) | Floating | 启动循环 | Hidden | Hidden 视觉 | null |
| T1 | 循环开始：设置过渡状态 | Floating | 广播 TransitionStarted | Hidden | Hidden 视觉 | Floating |
| T2 | 循环：开始动画 Hidden → Floating | Floating | 插值中 | Hidden | 插值 30% | Floating |
| T3 | TransitionTo(Fullscreen) | Fullscreen | 检测到变化，取消动画 | Hidden | 插值 30%（停留） | Floating |
| T4 | 循环：清除过渡状态 | Fullscreen | 继续 | Hidden | 插值 30% | null |
| T5 | 循环：设置过渡状态 | Fullscreen | 广播 TransitionStarted | Hidden | 插值 30% | Fullscreen |
| T6 | 循环：开始动画 30% → Fullscreen | Fullscreen | 插值中 | Hidden | 插值 50% | Fullscreen |
| T7 | TransitionTo(Sidebar) | Sidebar | 检测到变化，取消动画 | Hidden | 插值 50%（停留） | Fullscreen |
| T8 | 循环：清除过渡状态 | Sidebar | 继续 | Hidden | 插值 50% | null |
| T9 | 循环：设置过渡状态 | Sidebar | 广播 TransitionStarted | Hidden | 插值 50% | Sidebar |
| T10 | 循环：开始动画 50% → Sidebar | Sidebar | 插值中 | Hidden | 插值进行中 | Sidebar |
| T11 | 循环：动画完成 | Sidebar | 成功 | Sidebar | Sidebar 视觉 | null |
| T12 | 广播 StateChanged(Hidden, Sidebar) | Sidebar | 循环结束 | Sidebar | Sidebar 视觉 | null |

**实际执行的动画序列：**

- Hidden 视觉 → Floating 视觉（播放到 30%）❌ 被打断
- 30% 中间视觉 → Fullscreen 视觉（播放到 50%）❌ 被打断
- 50% 中间视觉 → Sidebar 视觉（最终成功）✅

**关键优势：**

- **请求压缩**：中间的 Floating、Fullscreen 请求被自动"压缩"掉，只执行最后的 Sidebar
- **无锁设计**：单线程循环，无需加锁
- **零延迟打断**：TransitionTo 使用 `Interlocked.Exchange` 原子替换 CTS，零 CPU 开销
- **资源安全**：快照 + 条件释放模式，防止误释放或重复释放 CTS
- **无需反向动画**：打断时直接从中间视觉状态继续，像 iOS 手势那样流畅
- **过渡状态可见**：通过 `TransitioningTo` 属性和 `TransitionStarted` 事件，外部可以实时了解转换进度
- **视觉连贯性**：所有过渡都在统一视觉空间中插值，无跳变

### 4.4 资源管理策略

**CancellationTokenSource 的所有权模型：**

当前设计采用"快照 + 条件释放"模式：
- TransitionTo: 创建新 CTS，原子替换旧 CTS，取消并释放旧 CTS
- RunStateMachineLoop: 快照当前 CTS，在 finally 中条件释放

**权衡说明：**
- ✅ 安全性：通过 `if (animationCts == _currentCts)` 判断，防止误释放新一轮的 CTS
- ⚠️ 轻微泄漏风险：如果在 finally 执行前 CTS 被替换，旧 CTS 可能不会被释放
- 📊 实际影响：CancellationTokenSource 很轻量，无非托管资源，GC 会快速回收

**未来优化方向（可选）：**

更严格的所有权模型：
```csharp
// TransitionTo 负责释放旧 CTS
var old = Interlocked.Exchange(ref _currentCts, newCts);
old?.Cancel();
old?.Dispose();

// RunStateMachineLoop 只使用，不释放全局 CTS
finally {
    // 移除条件判断，直接释放自己的快照
    animationCts.Dispose();
}
```

这种方式明确了"谁创建，谁释放"的责任，但需要确保没有地方在 Dispose 后使用 Token。

## 五、WindowService (Win32抽象层)

静态函数集，无状态，专门将Win32 API翻译为语义清晰的函数。上层任何模块均可直接调用。

```csharp
/// <summary>
/// Win32 API 静态抽象层，无状态，纯函数集
/// </summary>
static class WindowService
{
    // - 样式操作 -
    // 去除标题栏（移除 WS_CAPTION / WS_SYSMENU 样式）
    public static void RemoveTitleBar(IntPtr hwnd) { }
    // 设置透明背景（配合 DWM 亚克力效果使用）
    public static void SetTransparentBackground(IntPtr hwnd) { }
    // 设置扩展窗口样式（如 WS_EX_TOOLWINDOW）
    public static void SetExtendedStyle(IntPtr hwnd, int exStyle) { }

    // - 层叠/可见性 -
    // 设置置顶（HWND_TOPMOST / HWND_NOTOPMOST）
    public static void SetTopmost(IntPtr hwnd, bool topmost) { }
    // 显示窗口
    public static void ShowWindow(IntPtr hwnd) { }
    // 隐藏窗口
    public static void HideWindow(IntPtr hwnd) { }

    // - 位置/尺寸 -
    // 移动窗口到指定坐标
    public static void MoveWindow(IntPtr hwnd, int x, int y) { }
    // 调整窗口大小
    public static void ResizeWindow(IntPtr hwnd, int width, int height) { }

    // - DWM -
    // 设置 DWM 属性（如圆角、亚克力参数）
    public static void SetDwmAttribute(IntPtr hwnd, int attribute, int value) { }
}
```

## 六、首次创建流程 (Initializing状态)

WinUI3 要求调用 Activate() 才能获取 HWND，且内部会强制显示窗口，行为无法拦截。

**关键设计原则：Activate() 后窗口保持显示，不应隐藏**

```csharp
// App启动时执行
var window = new MainWindow();

// 在 Activate() 之前完成所有初始化设置
// 例如：设置初始位置、尺寸、样式等
WindowService.SetInitialBounds(window.GetHwnd(), initialBounds);
WindowService.SetInitialStyle(window.GetHwnd());

// 调用 Activate() - WinUI3 强制显示窗口
window.Activate(); // 此时窗口可见，视为 Initializing 状态

// 第一帧完成后，状态机接管，转换到目标可见状态（如 Floating）
DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
    // 根据应用逻辑决定初始状态
    // 通常是 Floating、Fullscreen 或 Sidebar，而不是 Hidden
    stateManager.TransitionTo(WindowState.Floating);
});
```

**重要说明：**
- Initializing 是唯一起点，代表 WinUI3 内部的初始化阶段
- Activate() 调用后窗口已显示，不应该转换到 Hidden 状态
- 应该直接转换到目标可见状态（Floating/Fullscreen/Sidebar）
- 如果应用启动时需要隐藏窗口，应该在 Activate() 之前通过 Win32 API 设置窗口样式

**如果确实需要启动时隐藏窗口：**

```csharp
var window = new MainWindow();

// 方案1：在 Activate() 前设置初始透明度为 0
window.Opacity = 0;
window.Activate();

// 然后根据需要显示
DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
    if (shouldShowOnStartup) {
        stateManager.TransitionTo(WindowState.Floating);
    } else {
        // 保持透明，或者转换到 Hidden
        stateManager.TransitionTo(WindowState.Hidden);
    }
});

// 方案2：在 Activate() 前通过 Win32 API 移除 WS_VISIBLE 样式
// （需要在 Activate() 前获取 HWND，可能需要特殊处理）
```

| 触发时机 | 应用启动，new Window() + Activate() 调用时 |
|---------|----------------------------------------|
| 持续时长 | 第一帧渲染完成前（WinUI3 内部控制） |
| 退出方式 | 转换到目标可见状态（通常是 Floating/Fullscreen/Sidebar） |
| 限制 | 唯一起点，不可从其他任何状态转入 |
| 特殊情况 | 如需启动时隐藏，应在 Activate() 前设置 Opacity=0 或 Win32 样式 |

## 七、表现层各形态说明

三个窗口形态各自实现 IWindowState接口，定义目标视觉和动画偏好，互不依赖。

### 7.1 FloatingWindow - 浮窗模式

小型悬浮窗口，可自由拖动。

```csharp
class FloatingWindow : IWindowState
{
    public WindowVisualState GetTargetVisual() {
        return new WindowVisualState {
            Bounds = new Rect(100, 100, 400, 600),
            CornerRadius = 12,
            Opacity = 1.0,
            IsTopmost = true,
            ExtendedStyle = WS_EX_TOOLWINDOW
        };
    }

    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to) {
        // 根据距离动态调整动画时长
        var distance = CalculateDistance(from.Bounds, to.Bounds);
        var duration = distance < 100 
            ? TimeSpan.FromMilliseconds(150) 
            : TimeSpan.FromMilliseconds(300);

        return new AnimationSpec {
            Duration = duration,
            Easing = Easing.EaseInOutCubic
        };
    }

    public void OnEnter() {
        // 进入浮窗模式前，确保窗口可见和可交互
        window.IsVisible = true;
        window.IsHitTestVisible = true;
    }

    public void OnExit() {
        // 离开浮窗模式时无需特殊处理
    }
}
```

| 属性 | 描述 |
|------|------|
| 目标位置 | 屏幕左上角偏移 (100, 100) |
| 目标尺寸 | 400x600 |
| 圆角 | 12px |
| 不透明度 | 1.0（完全不透明）|
| 特殊行为 | 支持拖动，置顶显示 |

### 7.2 FullscreenWindow - 全屏模式

覆盖整个屏幕的展开视图。

```csharp
class FullscreenWindow : IWindowState
{
    public WindowVisualState GetTargetVisual() {
        var screen = GetCurrentScreen();
        return new WindowVisualState {
            Bounds = screen.Bounds,
            CornerRadius = 0,
            Opacity = 1.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };
    }

    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to) {
        // 如果从很小的窗口放大，使用 Spring 更自然
        var sizeRatio = to.Bounds.Width / from.Bounds.Width;
        
        if (sizeRatio > 2.0) {
            return new AnimationSpec {
                Duration = TimeSpan.FromMilliseconds(400),
                Spring = new SpringConfig {
                    Stiffness = 300,
                    Damping = 30
                }
            };
        }

        return new AnimationSpec {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = Easing.EaseInOutCubic
        };
    }

    public void OnEnter() {
        window.IsVisible = true;
        window.IsHitTestVisible = true;
        // 可选：隐藏任务栏
    }

    public void OnExit() {
        // 可选：恢复任务栏
    }
}
```

| 属性 | 描述 |
|------|------|
| 目标位置 | (0, 0) |
| 目标尺寸 | 屏幕完整尺寸 |
| 圆角 | 0px |
| 不透明度 | 1.0（完全不透明）|
| 特殊行为 | 支持多显示器，进入时隐藏任务栏 |

### 7.3 SidebarWindow - 边栏模式

吸附在屏幕边缘的固定侧边栏。

```csharp
class SidebarWindow : IWindowState
{
    public WindowVisualState GetTargetVisual() {
        var screen = GetCurrentScreen();
        return new WindowVisualState {
            Bounds = new Rect(screen.Bounds.Right - 400, 0, 400, screen.Bounds.Height),
            CornerRadius = 0,
            Opacity = 1.0,
            IsTopmost = false,
            ExtendedStyle = WS_EX_APPBAR
        };
    }

    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to) {
        return new AnimationSpec {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = Easing.EaseOutCubic
        };
    }

    public void OnEnter() {
        window.IsVisible = true;
        window.IsHitTestVisible = true;
        // 注册为 AppBar，占用屏幕工作区
    }

    public void OnExit() {
        // 取消 AppBar 注册
    }
}
```

| 属性 | 描述 |
|------|------|
| 目标位置 | 屏幕右边缘 |
| 目标尺寸 | 400 x 屏幕高度 |
| 圆角 | 0px |
| 不透明度 | 1.0（完全不透明）|
| 特殊行为 | 注册为 AppBar，占用屏幕工作区 |

### 7.4 HiddenWindow - 隐藏状态

窗口完全隐藏，不可见也不可交互。

```csharp
class HiddenWindow : IWindowState
{
    public WindowVisualState GetTargetVisual() {
        // 保持当前位置和尺寸，只改变透明度
        return new WindowVisualState {
            Bounds = _currentVisual.Bounds,
            CornerRadius = _currentVisual.CornerRadius,
            Opacity = 0.0,  // 完全透明
            IsTopmost = false,
            ExtendedStyle = 0
        };
    }

    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to) {
        return new AnimationSpec {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = Easing.EaseOutCubic
        };
    }

    public void OnEnter() {
        // 动画开始前不改变 IsVisible，让渐隐动画可见
    }

    public void OnExit() {
        // 动画完成后，从视觉树中移除
        window.IsVisible = false;
        window.IsHitTestVisible = false;
    }
}
```

**隐藏/显示动画流程：**

显示动画（Hidden → Floating）：
1. OnEnter: 设置 IsVisible = true（窗口进入视觉树）
2. 动画: Opacity 0.0 → 1.0（渐显）
3. 完成: 窗口完全可见

隐藏动画（Floating → Hidden）：
1. OnEnter: 保持 IsVisible = true（让动画可见）
2. 动画: Opacity 1.0 → 0.0（渐隐）
3. OnExit: 设置 IsVisible = false（从视觉树移除）

## 八、AnimationEngine（统一动画引擎）

负责执行所有视觉状态插值，支持线性插值（LERP）和 Spring 物理模拟。

### 8.1 核心职责

- 从当前视觉状态平滑过渡到目标视觉状态
- 支持多种插值策略（线性、缓动函数、Spring）
- 实时更新窗口外观
- 响应取消信号，立即停止动画

### 8.2 接口设计

```csharp
class AnimationEngine
{
    /// <summary>
    /// 执行动画，从 from 插值到 to
    /// </summary>
    /// <param name="from">起始视觉状态</param>
    /// <param name="to">目标视觉状态</param>
    /// <param name="spec">动画规格</param>
    /// <param name="onProgress">进度回调，实时更新视觉状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task Animate(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken
    ) {
        if (spec.Spring != null) {
            await AnimateWithSpring(from, to, spec, onProgress, cancellationToken);
        } else {
            await AnimateWithEasing(from, to, spec, onProgress, cancellationToken);
        }
    }

    private async Task AnimateWithEasing(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken
    ) {
        // 使用 Stopwatch 实现时间驱动（而非帧驱动）
        var stopwatch = Stopwatch.StartNew();
        var duration = spec.Duration;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            // 根据真实时间计算进度（而非帧数）
            var elapsed = stopwatch.Elapsed;
            var progress = Math.Min(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);
            var easedProgress = spec.Easing.Apply(progress);

            // 插值所有属性
            var current = Lerp(from, to, easedProgress);
            onProgress(current);

            if (progress >= 1.0) break;

            await Task.Delay(16, cancellationToken); // ~60 FPS
        }
    }

    private async Task AnimateWithSpring(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken
    ) {
        // Spring 物理模拟实现
        // 使用 spec.Spring.Stiffness 和 spec.Spring.Damping
        // ...
    }

    private WindowVisualState Lerp(WindowVisualState from, WindowVisualState to, double t) {
        return new WindowVisualState {
            Bounds = new Rect(
                Lerp(from.Bounds.X, to.Bounds.X, t),
                Lerp(from.Bounds.Y, to.Bounds.Y, t),
                Lerp(from.Bounds.Width, to.Bounds.Width, t),
                Lerp(from.Bounds.Height, to.Bounds.Height, t)
            ),
            CornerRadius = Lerp(from.CornerRadius, to.CornerRadius, t),
            Opacity = Lerp(from.Opacity, to.Opacity, t),
            IsTopmost = to.IsTopmost,
            ExtendedStyle = to.ExtendedStyle
        };
    }

    private double Lerp(double a, double b, double t) => a + (b - a) * t;
}
```

**关键改进：时间驱动 vs 帧驱动**

- ❌ 旧方式（帧驱动）：每帧 progress += delta，依赖固定帧率
- ✅ 新方式（时间驱动）：根据 Stopwatch 计算真实进度
- 优势：即使掉帧，动画仍然准时完成，只是少几帧，不会变慢

### 8.3 设计优势

- **统一控制**：所有动画逻辑集中在一处，易于调试和优化
- **可扩展**：支持添加新的插值策略（如弹性、回弹等）
- **高性能**：可优化为使用 CompositionAnimation 在 Composition 线程执行
- **全局策略**：可轻松实现全局动画速度调整、性能模式等
- **时间精度**：使用 Stopwatch 确保动画时长准确，不受帧率波动影响

### 8.4 动画引擎演进路线

**Phase 1（当前实现）：Task.Delay + Stopwatch**
- ✅ 实现简单，行为正确
- ✅ 时间驱动，不依赖固定帧率
- ⚠️ 在 UI 线程执行，可能受 UI 阻塞影响

**Phase 2（中期优化）：DispatcherQueueTimer 或 CompositionTarget.Rendering**
- ✅ 更贴近帧同步
- ✅ 不用 async loop，结构更清晰
- ⚠️ 仍在 UI 线程，精度有限

**Phase 3（成熟版本）：CompositionAnimation**
- ✅ 在 Composition 线程执行，完全不卡 UI
- ✅ 帧率由系统保证（接近 vsync）
- ✅ 支持硬件加速
- ⚠️ 需要将动画拆成 Visual/SpriteVisual + KeyFrame Animation
- ⚠️ 复杂逻辑（如打断、反向）实现难度较高

**推荐策略：**
- 关键动画（位移、透明度、缩放）迁移到 CompositionAnimation
- 状态机只负责"发命令"，不参与具体插值
- 通过 IAnimationDriver 抽象，支持多种引擎实现

## 九、全局动画策略（IAnimationPolicy）

为了解决快速切换状态时动画参数不一致的问题，引入全局动画策略层。

### 9.1 接口定义

```csharp
/// <summary>
/// 全局动画策略接口，统一管理动画参数
/// </summary>
interface IAnimationPolicy
{
    /// <summary>
    /// 根据状态转换和当前视觉状态，解析出最终的动画规格
    /// </summary>
    /// <param name="fromState">起始稳定状态（非中间状态）</param>
    /// <param name="toState">目标状态</param>
    /// <param name="currentVisual">当前实际视觉状态（可能是中间状态）</param>
    AnimationSpec Resolve(WindowState fromState, WindowState toState, WindowVisualState currentVisual);
}
```

### 9.2 默认策略实现

```csharp
class DefaultAnimationPolicy : IAnimationPolicy
{
    public AnimationSpec Resolve(WindowState fromState, WindowState toState, WindowVisualState currentVisual)
    {
        // 1. 防抖：如果距离上次转换很近，使用快速动画
        if (TimeSinceLastTransition < TimeSpan.FromMilliseconds(100))
        {
            return new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(120),
                Easing = Easing.Linear
            };
        }

        // 2. 根据转换类型选择动画
        var transitionType = GetTransitionType(fromState, toState);
        
        return transitionType switch
        {
            TransitionType.EnterFullscreen => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(400),
                Spring = new SpringConfig { Stiffness = 300, Damping = 30 }
            },
            TransitionType.ExitFullscreen => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(300),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.DockToSidebar => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.Float => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseInOutCubic
            },
            TransitionType.Hide => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseOutCubic
            },
            TransitionType.Show => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseInCubic
            },
            _ => new AnimationSpec
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = Easing.EaseInOutCubic
            }
        };
    }

    private TransitionType GetTransitionType(WindowState from, WindowState to)
    {
        return (from, to) switch
        {
            (_, WindowState.Fullscreen) => TransitionType.EnterFullscreen,
            (WindowState.Fullscreen, _) => TransitionType.ExitFullscreen,
            (_, WindowState.Sidebar) => TransitionType.DockToSidebar,
            (_, WindowState.Floating) => TransitionType.Float,
            (_, WindowState.Hidden) => TransitionType.Hide,
            (WindowState.Hidden, _) => TransitionType.Show,
            _ => TransitionType.Default
        };
    }
}

enum TransitionType
{
    Default,
    EnterFullscreen,
    ExitFullscreen,
    DockToSidebar,
    Float,
    Hide,
    Show
}
```

### 9.3 设计优势

- **一致性**：同样的状态转换，每次动画参数都相同
- **可预测性**：用户体验稳定，不会因中间状态而变化
- **集中管理**：所有动画参数在一处定义，易于调整
- **语义化**：使用 TransitionType 而非数值差异，更符合设计意图
- **防抖动**：快速切换时自动使用短动画，避免"情绪不稳定"

### 9.4 使用方式

在 WindowStateManager 中注入策略：

```csharp
var stateManager = new WindowStateManager(
    animationEngine: new AnimationEngine(),
    animationPolicy: new DefaultAnimationPolicy()  // 可选，不传则使用状态自己的 GetAnimationSpec
);
```

策略优先级：
1. 如果提供了 IAnimationPolicy，优先使用策略的 Resolve 方法
2. 否则，回退到各状态实现的 GetAnimationSpec 方法

## 十、过渡状态使用场景

`TransitioningTo` 属性和 `TransitionStarted` 事件为外部模块提供了实时的转换进度信息。

### 10.1 典型使用场景

**1. UI 状态指示器**
```csharp
// 托盘图标或状态栏显示
stateManager.TransitionStarted += (from, to) => {
    statusBar.Text = $"切换中: {from} → {to}";
};

stateManager.StateChanged += (from, to) => {
    statusBar.Text = $"当前状态: {to}";
};
```

**2. 功能禁用/启用**
```csharp
// 根据过渡状态禁用冲突操作
bool CanDrag() {
    // 只有在稳定的 Floating 状态下才能拖动
    return stateManager.CurrentState == WindowState.Floating 
        && stateManager.TransitioningTo == null;
}
```

**3. 资源预加载**
```csharp
// 提前准备目标状态所需的资源
stateManager.TransitionStarted += (from, to) => {
    if (to == WindowState.Fullscreen) {
        PreloadFullscreenResources();
    }
};
```

**4. 日志和遥测**
```csharp
// 记录完整的状态转换生命周期
stateManager.TransitionStarted += (from, to) => {
    logger.Info($"开始转换: {from} → {to}");
};

stateManager.StateChanged += (from, to) => {
    logger.Info($"完成转换: {from} → {to}");
};
```

### 10.2 状态查询最佳实践

- **判断是否稳定**: `TransitioningTo == null` 表示没有正在进行的转换
- **判断目标状态**: 使用 `TransitioningTo ?? CurrentState` 获取"最终会到达的状态"
- **避免在转换中修改状态**: 如果 `TransitioningTo != null`，避免调用 `TransitionTo()`，除非确实需要打断

## 十一、正确性属性

*属性是系统在所有有效执行中都应该保持为真的特征或行为——本质上是关于系统应该做什么的形式化陈述。属性是人类可读规范和机器可验证正确性保证之间的桥梁。*

### 属性 1: 状态转换规则

*对于任意*起始状态和目标状态，状态机应该根据以下规则验证转换的合法性：
- 从 Initializing 只能转换到 Floating、Fullscreen、Sidebar 或 Hidden
- 从任何可见状态（Floating、Fullscreen、Sidebar）可以转换到任何其他状态
- 从 Hidden 可以转换到任何可见状态
- 不能从任何状态转换回 Initializing

**验证需求: 需求 1.3, 1.4, 1.5, 1.6**

### 属性 2: TransitionTo 立即返回

*对于任意*目标状态，调用 TransitionTo 方法应该在很短时间内（<10ms）返回，而不等待动画完成

**验证需求: 需求 2.1**

### 属性 3: 请求压缩

*对于任意*状态序列，如果快速连续调用 TransitionTo，最终的 CurrentState 应该等于最后一个目标状态，中间的请求应该被自动压缩

**验证需求: 需求 2.2**

### 属性 4: 动画取消

*对于任意*两个不同的目标状态，当第二次 TransitionTo 调用发出时，第一次调用的动画应该被立即取消

**验证需求: 需求 2.3, 15.1**

### 属性 5: 线程安全

*对于任意*目标状态，从后台线程调用 TransitionTo 应该成功执行且不抛出异常，调用会被自动转发到 UI 线程

**验证需求: 需求 2.4**

### 属性 6: 平滑插值

*对于任意*起始视觉状态和目标视觉状态，动画过程中的所有中间视觉状态应该在起始和目标之间（对于所有连续量属性如 Bounds、CornerRadius、Opacity）

**验证需求: 需求 3.1**

### 属性 7: 打断后连续性

*对于任意*动画，如果在执行过程中被打断，新动画的起始视觉状态应该是打断时刻的中间视觉状态，而不是原始起始状态

**验证需求: 需求 3.2, 15.2, 15.3**

### 属性 8: 时间精度

*对于任意*动画时长设置，实际执行时间应该接近设定值，误差应该小于 5%，即使在系统掉帧的情况下

**验证需求: 需求 3.4, 16.2, 16.3**

### 属性 9: 进度回调

*对于任意*动画，onProgress 回调应该被多次调用（至少 2 次），且每次调用的参数值应该在起始和目标视觉状态之间

**验证需求: 需求 3.5**

### 属性 10: 取消响应

*对于任意*正在执行的动画，调用 CancellationToken.Cancel() 后，动画应该在很短时间内（<50ms）停止执行

**验证需求: 需求 3.6**

### 属性 11: 视觉状态插值

*对于任意*两个 WindowVisualState 和插值进度 t（0.0 到 1.0），插值结果的每个连续量属性（Bounds、CornerRadius、Opacity）应该等于 `start + (end - start) * t`

**验证需求: 需求 4.2**

### 属性 12: 插值一致性

*对于任意*两个 WindowVisualState 和插值进度 t，所有连续量属性应该使用相同的 t 值进行插值，确保视觉变化的同步性

**验证需求: 需求 4.3**

### 属性 13: 事件通知顺序

*对于任意*状态转换，事件触发顺序应该是：TransitionStarted(from, to) → 动画执行 → StateChanged(from, to)，且事件参数应该正确反映起始和目标状态

**验证需求: 需求 8.1, 8.2**

### 属性 14: 稳定状态下 TransitioningTo 为空

*对于任意*稳定状态（没有正在进行的转换），TransitioningTo 属性应该返回 null

**验证需求: 需求 8.5**

### 属性 15: 动画策略一致性

*对于任意*相同的状态转换（相同的起始状态和目标状态），IAnimationPolicy 应该返回一致的动画规格（相同的 Duration、Easing 等参数）

**验证需求: 需求 10.1, 10.4**

### 属性 16: 资源释放

*对于任意*状态转换序列，每次创建新的 CancellationTokenSource 时，旧的 CTS 应该被取消并释放，不应该出现资源泄漏

**验证需求: 需求 11.1, 11.3**

### 属性 17: 渐显动画

*对于任意*从 Hidden 到可见状态的转换，Opacity 应该从 0.0 平滑增加到 1.0，且 IsVisible 应该在动画开始前设置为 true

**验证需求: 需求 14.1**

### 属性 18: 渐隐动画

*对于任意*从可见状态到 Hidden 的转换，Opacity 应该从 1.0 平滑减少到 0.0，且 IsVisible 应该在动画完成后设置为 false

**验证需求: 需求 14.2**

## 十二、待定/备注

- WindowService函数列表随开发推进持续补充，当前仅列出已确认需要的部分。
- 各状态实现类的具体动画参数（时长、缓动函数等）不在本文档范围内，可在实现时根据实际效果调整。
- Initializing→ Hidden的时机（DispatcherQueue优先级）如出现闪烁问题，需进一步调研。可考虑在 Activate() 前通过 Win32 API 预设窗口样式或初始透明度。

**动画引擎优化路线：**
- Phase 1（当前）：Task.Delay + Stopwatch 时间驱动
- Phase 2（中期）：DispatcherQueueTimer 或 CompositionTarget.Rendering
- Phase 3（成熟）：关键动画迁移到 CompositionAnimation，通过 IAnimationDriver 抽象支持多引擎

**首次创建流程：**
- Activate() 调用后窗口已显示，不应该再隐藏
- 通常从 Initializing 转换到 Floating/Fullscreen/Sidebar
- 如需启动时隐藏（如托盘应用），应在 Activate() 前设置 Opacity=0 或 Win32 样式
- 避免 Activate() 后立即隐藏导致的闪烁问题

**全局动画策略：**
- 考虑添加全局动画速度调整功能（如性能模式、无障碍模式）
- 考虑添加动画曲线可视化工具，方便调试和调优

**资源管理优化：**
- 当前 CancellationTokenSource 采用"快照 + 条件释放"模式，安全但可能轻微泄漏
- 未来可优化为更严格的所有权模型（TransitionTo 负责释放旧 CTS）

**动画语义层（高级特性）：**
- 引入 TransitionType 枚举，完全基于语义而非数值差异
- 与系统级动画保持一致（Windows/macOS 风格）
- 需要重构状态机，将转换类型作为一等公民

**测试策略：**
- WinUI3 UI 层需要手动测试
- 状态机逻辑可通过 mock IWindowService 进行单元测试（可选优化）


### 属性 19: 转换期间视觉指示器存在性

*对于任意*正在进行的状态转换（TransitioningTo != null），UI 应该包含视觉指示器元素（如加载动画或进度条），且在转换完成后（TransitioningTo == null）应该自动移除

**验证需求: 需求 21.1, 21.3, 21.4**

### 属性 20: 禁用操作的用户反馈

*对于任意*被禁用的操作（如在非 Floating 状态下拖动窗口），当用户尝试执行该操作时，系统应该显示临时提示信息说明操作不可用的原因

**验证需求: 需求 21.2**

### 属性 21: 操作可用性提示

*对于任意*窗口状态，鼠标悬停在操作控件上时应该显示 Tooltip，正确描述当前状态下哪些操作可用、哪些操作被禁用

**验证需求: 需求 21.5**

### 属性 22: 转换期间的请求反馈

*对于任意*正在进行的状态转换，如果用户尝试触发新的状态转换，系统应该显示简短的通知消息（如"请稍候，正在切换窗口模式"）

**验证需求: 需求 21.6**

### 属性 23: 异常捕获和日志记录

*对于任意*动画执行过程中发生的未预期异常，系统应该捕获该异常并在日志中记录详细的错误信息（包括异常类型、消息和堆栈跟踪）

**验证需求: 需求 22.1**

### 属性 24: 动画失败后的状态恢复

*对于任意*动画执行失败的情况，WindowStateManager 应该将 CurrentState 恢复到动画开始前的稳定状态

**验证需求: 需求 22.2**

### 属性 25: Win32 API 错误处理

*对于任意*Win32 API 调用失败的情况，WindowService 应该返回错误代码并记录失败原因，而不是抛出未处理的异常

**验证需求: 需求 22.3**

### 属性 26: 转换失败事件通知

*对于任意*状态转换过程中发生的异常，系统应该触发 TransitionFailed 事件，且事件参数应该包含起始状态、目标状态和异常信息

**验证需求: 需求 22.4**

### 属性 27: 显示器信息获取失败的后备方案

*对于任意*无法获取显示器信息的情况（如显示器被拔出），系统应该使用主显示器的尺寸和位置作为后备方案

**验证需求: 需求 22.5**

### 属性 28: 动画引擎初始化失败的降级处理

*对于任意*动画引擎初始化失败的情况，系统应该回退到无动画模式，状态转换应该立即完成且 CurrentState 正确更新

**验证需求: 需求 22.6**

### 属性 29: 错误恢复机制

*对于任意*窗口状态，调用错误恢复方法后，窗口应该成功转换到默认的 Floating 状态

**验证需求: 需求 22.7**

### 属性 30: 连续失败保护机制

*对于任意*连续失败的状态转换序列，当失败次数超过阈值（3 次）时，系统应该禁用自动状态转换并触发通知事件

**验证需求: 需求 22.8**

### 属性 31: 转换失败后的资源清理

*对于任意*状态转换失败的情况，WindowStateManager 应该清理所有中间资源（如 CancellationTokenSource），不应该出现资源泄漏

**验证需求: 需求 22.9**

### 属性 32: 无效窗口句柄的处理

*对于任意*动画执行过程中窗口句柄（HWND）变为无效的情况，系统应该立即停止动画并在日志中记录错误

**验证需求: 需求 22.10**

### 属性 33: 全屏按钮状态转换

*对于任意*初始窗口状态，当用户点击全屏按钮时，系统应该调用 WindowStateManager.TransitionTo(WindowState.Fullscreen)，且最终 CurrentState 应该等于 Fullscreen

**验证需求: 需求 23.1**

### 属性 34: 侧边栏按钮状态转换

*对于任意*初始窗口状态，当用户点击侧边栏按钮时，系统应该调用 WindowStateManager.TransitionTo(WindowState.Sidebar)，且最终 CurrentState 应该等于 Sidebar

**验证需求: 需求 23.2**

### 属性 35: 托盘按钮可见性切换

*对于任意*窗口状态，当用户点击任务栏托盘按钮时：
- 如果当前状态为 Hidden，系统应该恢复到隐藏前的可见状态（Floating、Fullscreen 或 Sidebar）
- 如果当前状态为可见状态，系统应该切换到 Hidden 状态

**验证需求: 需求 23.3, 23.9**

### 属性 36: 快捷键状态转换

*对于任意*初始窗口状态和快捷键，当用户触发快捷键时，系统应该调用对应的 WindowStateManager.TransitionTo 方法，且最终 CurrentState 应该等于目标状态

**验证需求: 需求 23.4**

### 属性 37: 转换期间 UI 控件禁用

*对于任意*正在进行的状态转换（TransitioningTo != null），所有触发状态转换的 UI 控件（按钮、快捷键等）应该显示禁用状态或加载指示器

**验证需求: 需求 23.6**

### 属性 38: 转换完成后 UI 控件视觉更新

*对于任意*状态转换完成后（TransitioningTo == null），所有 UI 控件的视觉状态应该正确反映当前窗口状态（如全屏按钮在 Fullscreen 状态下显示"退出全屏"文本或图标）

**验证需求: 需求 23.7**

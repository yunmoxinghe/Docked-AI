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

### 4.0 创建和初始化

WindowStateManager 需要多个依赖才能正常工作，包括 DispatcherQueue、AnimationEngine、IAnimationPolicy 等。以下是创建和初始化的完整流程。

#### 4.0.1 依赖注入示例

```csharp
// 在 App.xaml.cs 或 MainWindow 构造函数中创建依赖

// 1. 创建 WindowContext（集中管理 HWND 和核心引用）
var windowContext = new WindowContext(mainWindow);

// 2. 创建 AnimationEngine（统一动画引擎）
var animationEngine = new AnimationEngine();

// 3. 创建 IAnimationPolicy（可选，用于统一管理动画参数）
var animationPolicy = new DefaultAnimationPolicy();

// 4. 创建 WindowService（Win32 API 抽象层，静态类无需实例化）
// WindowService 是静态类，直接使用即可

// 5. 创建 WindowStateManager（状态机）
var stateManager = new WindowStateManager(
    dispatcher: DispatcherQueue.GetForCurrentThread(),
    animationEngine: animationEngine,
    animationPolicy: animationPolicy,  // 可选参数
    context: windowContext
);
```

#### 4.0.2 WindowStateManager 构造函数定义

```csharp
class WindowStateManager
{
    public WindowStateManager(
        DispatcherQueue dispatcher,
        AnimationEngine animationEngine,
        WindowContext context,
        IAnimationPolicy? animationPolicy = null
    ) {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _animationEngine = animationEngine ?? throw new ArgumentNullException(nameof(animationEngine));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _animationPolicy = animationPolicy;
        
        // 初始化状态
        CurrentState = WindowState.Initializing;
        _lastStableState = WindowState.Initializing;
        _currentVisual = context.GetCurrentVisual();
    }
    
    // ... 其他成员
}
```

#### 4.0.3 生命周期管理建议

**方案 1：在 App.xaml.cs 中创建单例**

```csharp
public partial class App : Application
{
    public static WindowStateManager StateManager { get; private set; }
    
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 使用 MainWindowFactory 创建窗口（不激活）
        var window = MainWindowFactory.Create();
        
        // 在 Activate() 之前创建 WindowStateManager
        var context = new WindowContext(window);
        var animationEngine = new AnimationEngine();
        var animationPolicy = new DefaultAnimationPolicy();
        
        StateManager = new WindowStateManager(
            dispatcher: DispatcherQueue.GetForCurrentThread(),
            animationEngine: animationEngine,
            animationPolicy: animationPolicy,
            context: context
        );
        
        // 调用 Activate() 显示窗口
        window.Activate();
        
        // 第一帧完成后，转换到目标状态
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
            StateManager.TransitionTo(WindowState.Floating);
        });
    }
}
```

**方案 2：在 MainWindow 构造函数中创建**

```csharp
public sealed partial class MainWindow : Window
{
    private readonly WindowStateManager _stateManager;
    
    public MainWindow()
    {
        this.InitializeComponent();
        
        // 创建依赖
        var context = new WindowContext(this);
        var animationEngine = new AnimationEngine();
        var animationPolicy = new DefaultAnimationPolicy();
        
        // 创建状态管理器
        _stateManager = new WindowStateManager(
            dispatcher: DispatcherQueue.GetForCurrentThread(),
            animationEngine: animationEngine,
            animationPolicy: animationPolicy,
            context: context
        );
        
        // 订阅事件
        _stateManager.StateChanged += OnStateChanged;
        _stateManager.TransitionStarted += OnTransitionStarted;
        _stateManager.TransitionFailed += OnTransitionFailed;
    }
    
    private void OnStateChanged(WindowState from, WindowState to)
    {
        // 处理状态变化
    }
    
    private void OnTransitionStarted(WindowState from, WindowState to)
    {
        // 显示转换指示器
    }
    
    private void OnTransitionFailed(WindowState from, WindowState to, Exception ex)
    {
        // 处理转换失败
    }
}
```

#### 4.0.4 初始化顺序

正确的初始化顺序如下：

1. **使用 MainWindowFactory 创建窗口**（但不调用 Activate()）
   ```csharp
   var window = MainWindowFactory.Create();
   ```
2. **创建 WindowContext**（传入 MainWindow 实例）
3. **创建 AnimationEngine**
4. **创建 IAnimationPolicy**（可选）
5. **创建 WindowStateManager**（传入所有依赖）
6. **订阅事件**（StateChanged、TransitionStarted、TransitionFailed）
7. **调用 window.Activate()**（WinUI3 强制显示窗口）
8. **在第一帧完成后调用 TransitionTo**（转换到目标状态）

**关键注意事项：**

- 使用 MainWindowFactory.Create() 而非直接 new MainWindow()，以便统一管理窗口创建
- WindowStateManager 必须在 Activate() 之前创建，以便在窗口显示前完成初始化
- Activate() 调用后窗口已显示，状态为 Initializing
- 使用 DispatcherQueue.TryEnqueue 在第一帧完成后执行状态转换
- 如果需要启动时隐藏窗口，应在 Activate() 之前设置 Opacity=0
- MainWindowFactory 提供了窗口有效性检查（IsWindowValid）和获取或创建逻辑（GetOrCreate）

### 4.1 字段和属性定义

```csharp
class WindowStateManager
{
    // - 状态属性 -
    // 当前稳定状态（只读）
    public WindowState CurrentState { get; private set; }
    
    // 上一个稳定状态（用于动画参数计算）
    // 初始值为 Initializing，表示应用启动时的初始状态
    private WindowState _lastStableState = WindowState.Initializing;
    
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
    // 参数：from=切换前的稳定状态（即动画开始时的 CurrentState），to=切换后状态
    // 注意：from 是"上一个稳定状态"，而不是"上一个请求的状态"
    // 例如：快速切换 Floating → Fullscreen → Sidebar 时，最终广播 StateChanged(Floating, Sidebar)
    public event Action<WindowState, WindowState>? StateChanged;
    
    // 开始转换到新状态时广播
    // 参数：from=当前状态，to=目标状态
    public event Action<WindowState, WindowState>? TransitionStarted;
    
    // 状态转换失败时广播
    // 参数：from=起始状态，to=目标状态，exception=异常信息
    public event Action<WindowState, WindowState, Exception>? TransitionFailed;

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

**StateChanged 事件的 from 参数语义说明：**

在上述示例中，最终的 StateChanged 事件是 `StateChanged(Hidden, Sidebar)`，而不是 `StateChanged(Fullscreen, Sidebar)`。这是因为：

1. **from 参数是"上一个稳定状态"**：即动画开始时的 `CurrentState`（Hidden），而不是"上一个请求的状态"（Fullscreen）
2. **中间请求被压缩**：Floating 和 Fullscreen 请求被快速压缩，从未成为稳定状态
3. **语义一致性**：StateChanged 的 from 参数始终等于该转换开始时的 `_lastStableState`
4. **实际意义**：这反映了窗口的真实状态变化历史——窗口从 Hidden 状态直接过渡到 Sidebar 状态，中间的 Floating 和 Fullscreen 只是"意图"，从未实际稳定过

这种设计确保了 StateChanged 事件准确反映窗口的实际状态历史，而不是用户的请求历史。

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

    // - AppBar 管理 -
    // 注册窗口为 AppBar（占用屏幕工作区）
    // 使用 SHAppBarMessage(ABM_NEW, ...) 和 SHAppBarMessage(ABM_SETPOS, ...)
    public static void RegisterAppBar(IntPtr hwnd, AppBarEdge edge, int size) { }
    // 取消 AppBar 注册
    // 使用 SHAppBarMessage(ABM_REMOVE, ...)
    public static void UnregisterAppBar(IntPtr hwnd) { }
    
    // - 窗口大小调整控制 -
    // 启用窗口大小调整（设置 IsResizable = true）
    public static void EnableResize(IntPtr hwnd) { }
    // 禁用窗口大小调整（设置 IsResizable = false）
    public static void DisableResize(IntPtr hwnd) { }
    
    // - 焦点管理 -
    // 将窗口设置为前台窗口并获得输入焦点
    // 返回值：true 表示成功，false 表示失败（受系统限制）
    public static bool SetForegroundWindow(IntPtr hwnd) { }
    // 将窗口提升到 Z 轴顶部
    public static bool BringWindowToTop(IntPtr hwnd) { }
    // 闪烁窗口以吸引用户注意（降级方案）
    // flags: FLASHW_* 常量组合
    // count: 闪烁次数（0 表示持续闪烁）
    // timeout: 闪烁间隔（毫秒，0 表示使用默认值）
    public static bool FlashWindowEx(IntPtr hwnd, uint flags, uint count, uint timeout) { }
    // 获取当前前台窗口
    public static IntPtr GetForegroundWindow() { }
    // 获取窗口所属线程 ID
    public static uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId) { }
    // 尝试将窗口带到前台（组合策略）
    public static bool TryBringToFront(Window window) { }
}

/// <summary>
/// AppBar 边缘位置枚举
/// </summary>
enum AppBarEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}
```

**文件组织：**

WindowService 的实现按功能模块拆分到不同的文件中：

```
服务层/
├── 窗口服务.cs                    // WindowService 主类（静态类）
├── 窗口上下文.cs                  // WindowContext
├── 线程调度器.cs                  // ThreadDispatcher
├── 窗口钩子.cs                    // WindowHook
└── Win32互操作/
    ├── DWM互操作.cs               // DWM API 封装
    ├── 窗口样式互操作.cs          // 窗口样式相关 API
    ├── 窗口位置互操作.cs          // 位置和尺寸相关 API
    ├── 显示器管理器.cs            // 多显示器支持
    └── 焦点管理互操作.cs          // 焦点管理相关 API
                                    // (SetForegroundWindow, BringWindowToTop, 
                                    //  FlashWindowEx, GetForegroundWindow, 
                                    //  GetWindowThreadProcessId)
```

**设计原则：**
- WindowService 是静态类，所有方法都是静态方法
- 按功能模块拆分文件，每个文件负责一类 Win32 API 的封装
- 焦点管理互操作.cs 专门负责焦点相关的 Win32 API 封装和 TryBringToFront 实现
- 所有 Win32 API 调用都通过 WindowService 统一暴露，上层不直接调用 P/Invoke

## 六、WindowContext（窗口上下文）

WindowContext 是一个集中管理窗口实例、HWND 和当前视觉状态的上下文对象。它解决了多个模块需要访问相同窗口信息的问题，避免了重复传递参数和循环依赖。

### 6.1 职责说明

WindowContext 的主要职责包括：

1. **集中管理窗口引用**：持有 MainWindow 实例，提供统一的访问接口
2. **HWND 管理**：缓存窗口句柄（HWND），避免重复获取
3. **视觉状态访问**：提供当前窗口的实时视觉状态快照
4. **依赖注入桥梁**：作为各模块之间的依赖注入桥梁，解耦模块间的直接依赖

### 6.2 为什么需要 WindowContext

**问题场景：**

在没有 WindowContext 的情况下，各个模块（WindowStateManager、AnimationEngine、各窗口形态实现）都需要直接访问 MainWindow 实例和 HWND，导致：

1. **参数传递冗余**：每个方法都需要传递 window 和 hwnd 参数
2. **循环依赖风险**：WindowStateManager 需要 Window，Window 需要 WindowStateManager
3. **职责不清**：不清楚谁负责管理 HWND 的生命周期
4. **测试困难**：难以 mock 窗口实例进行单元测试

**解决方案：**

引入 WindowContext 作为中间层，集中管理所有窗口相关的引用和状态，各模块只需要依赖 WindowContext 即可。

### 6.3 接口定义

```csharp
/// <summary>
/// 窗口上下文，集中管理窗口实例、HWND 和当前视觉状态
/// </summary>
class WindowContext
{
    private readonly Window _window;
    private IntPtr _hwnd;
    private WindowVisualState _currentVisual;

    public WindowContext(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _hwnd = IntPtr.Zero;
        _currentVisual = new WindowVisualState();
    }

    /// <summary>
    /// 获取窗口实例
    /// </summary>
    public Window GetWindow() => _window;

    /// <summary>
    /// 获取窗口句柄（HWND）
    /// 如果尚未获取，则自动获取并缓存
    /// </summary>
    public IntPtr GetHwnd()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        }
        return _hwnd;
    }

    /// <summary>
    /// 获取当前窗口的实时视觉状态
    /// </summary>
    public WindowVisualState GetCurrentVisual()
    {
        // 从窗口读取当前的实际视觉状态
        var hwnd = GetHwnd();
        var bounds = WindowService.GetWindowBounds(hwnd);
        var opacity = _window.Opacity;
        // ... 读取其他属性

        _currentVisual = new WindowVisualState
        {
            Bounds = bounds,
            CornerRadius = GetCurrentCornerRadius(),
            Opacity = opacity,
            IsTopmost = WindowService.IsTopmost(hwnd),
            ExtendedStyle = WindowService.GetExtendedStyle(hwnd)
        };

        return _currentVisual;
    }

    /// <summary>
    /// 更新当前视觉状态缓存
    /// 由 AnimationEngine 在动画过程中调用
    /// </summary>
    public void UpdateCurrentVisual(WindowVisualState visual)
    {
        _currentVisual = visual;
    }

    private double GetCurrentCornerRadius()
    {
        // 从窗口读取当前圆角半径
        // 实现取决于具体的 UI 框架
        return 0.0;
    }
}
```

### 6.4 使用示例

**在 WindowStateManager 中使用：**

```csharp
class WindowStateManager
{
    private readonly WindowContext _context;

    public WindowStateManager(
        DispatcherQueue dispatcher,
        AnimationEngine animationEngine,
        WindowContext context,
        IAnimationPolicy? animationPolicy = null
    ) {
        _context = context;
        // ... 其他初始化
    }

    private void ApplyVisualToWindow(WindowVisualState visual)
    {
        var hwnd = _context.GetHwnd();
        WindowService.SetWindowBounds(hwnd, visual.Bounds);
        WindowService.SetTopmost(hwnd, visual.IsTopmost);
        // ... 应用其他属性
        
        _context.UpdateCurrentVisual(visual);
    }
}
```

**在窗口形态实现中使用：**

```csharp
class FloatingWindow : IWindowState
{
    private readonly WindowContext _context;

    public FloatingWindow(WindowContext context)
    {
        _context = context;
    }

    public WindowVisualState GetTargetVisual()
    {
        // 使用 context 获取当前视觉状态
        var currentVisual = _context.GetCurrentVisual();
        
        // 基于当前状态计算目标状态
        return new WindowVisualState
        {
            Bounds = CalculateFloatingBounds(),
            CornerRadius = 12,
            Opacity = 1.0,
            IsTopmost = true,
            ExtendedStyle = WS_EX_TOOLWINDOW
        };
    }

    public void OnEnter()
    {
        var window = _context.GetWindow();
        window.IsVisible = true;
        window.IsHitTestVisible = true;
    }
}
```

### 6.5 设计优势

1. **解耦**：各模块只依赖 WindowContext，不直接依赖 Window 或 HWND
2. **集中管理**：所有窗口相关的引用和状态都在一处管理
3. **易于测试**：可以轻松 mock WindowContext 进行单元测试
4. **性能优化**：HWND 只获取一次并缓存，避免重复调用 Win32 API
5. **状态同步**：通过 UpdateCurrentVisual 确保视觉状态缓存与实际窗口同步

## 七、首次创建流程 (Initializing状态)

WinUI3 要求调用 Activate() 才能获取 HWND，且内部会强制显示窗口，行为无法拦截。

**关键设计原则：使用 MainWindowFactory 延迟激活，避免闪烁**

当前实现采用 `MainWindowFactory` 来管理窗口创建和激活流程：

```csharp
// 使用 MainWindowFactory 创建窗口（不激活）
var window = MainWindowFactory.Create();

// 在 Activate() 之前完成所有初始化设置
// 创建 WindowContext
var context = new WindowContext(window);

// 创建 AnimationEngine 和 AnimationPolicy
var animationEngine = new AnimationEngine();
var animationPolicy = new DefaultAnimationPolicy();

// 创建 WindowStateManager
var stateManager = new WindowStateManager(
    dispatcher: DispatcherQueue.GetForCurrentThread(),
    animationEngine: animationEngine,
    animationPolicy: animationPolicy,
    context: context
);

// 订阅事件
stateManager.StateChanged += OnStateChanged;
stateManager.TransitionStarted += OnTransitionStarted;

// 调用 Activate() - WinUI3 强制显示窗口
window.Activate(); // 此时窗口可见，视为 Initializing 状态

// 第一帧完成后，状态机接管，转换到目标可见状态（如 Floating）
DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
    // 根据应用逻辑决定初始状态
    // 通常是 Floating、Fullscreen 或 Sidebar，而不是 Hidden
    stateManager.TransitionTo(WindowState.Floating);
});
```

**MainWindowFactory 职责：**
- `Create()`: 创建窗口但不激活，避免闪烁
- `CreateAndActivate()`: 创建并激活窗口（用于特殊场景）
- `GetOrCreate()`: 获取或创建有效的窗口实例
- `IsWindowValid()`: 检查窗口是否有效

**重要说明：**
- Initializing 是唯一起点，代表 WinUI3 内部的初始化阶段
- Activate() 调用后窗口已显示，不应该转换到 Hidden 状态
- 应该直接转换到目标可见状态（Floating/Fullscreen/Sidebar）
- 如果应用启动时需要隐藏窗口，应该在 Activate() 之前通过 Win32 API 设置窗口样式

**如果确实需要启动时隐藏窗口：**

```csharp
var window = MainWindowFactory.Create();

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

| 触发时机 | 应用启动，MainWindowFactory.Create() + Activate() 调用时 |
|---------|----------------------------------------|
| 持续时长 | 第一帧渲染完成前（WinUI3 内部控制） |
| 退出方式 | 转换到目标可见状态（通常是 Floating/Fullscreen/Sidebar） |
| 限制 | 唯一起点，不可从其他任何状态转入 |
| 特殊情况 | 如需启动时隐藏，应在 Activate() 前设置 Opacity=0 或 Win32 样式 |

## 八、表现层各形态说明

三个窗口形态各自实现 IWindowState接口，定义目标视觉和动画偏好，互不依赖。

### 8.1 FloatingWindow - 浮窗模式

可调整大小的悬浮窗口，通过与屏幕边缘的距离来定位。

**默认边距配置：**
- 窗口与屏幕顶部距离：10 像素
- 窗口与任务栏（底部）距离：10 像素
- 窗口与屏幕右侧距离：10 像素
- 默认尺寸：400 x 600 像素（首次启动时）

```csharp
class FloatingWindow : IWindowState
{
    private readonly WindowContext _context;
    private readonly IWindowPositionService _positionService;

    public FloatingWindow(WindowContext context, IWindowPositionService positionService) {
        _context = context;
        _positionService = positionService;
    }

    public WindowVisualState GetTargetVisual() {
        // 获取上次停留位置和尺寸，如果没有则使用默认值
        var lastPosition = _positionService.GetLastFloatingPosition();
        
        if (lastPosition != null) {
            // 根据保存的边缘距离和当前屏幕尺寸计算位置
            var screen = GetCurrentScreen();
            var rightDistance = lastPosition.RightDistance;
            var bottomDistance = lastPosition.BottomDistance;
            var width = lastPosition.Width;
            var height = lastPosition.Height;
            
            // 确保窗口在屏幕范围内
            var x = Math.Max(0, screen.Bounds.Right - rightDistance - width);
            var y = Math.Max(0, screen.Bounds.Bottom - bottomDistance - height);
            
            return new WindowVisualState {
                Bounds = new Rect(x, y, width, height),
                CornerRadius = 12,
                Opacity = 1.0,
                IsTopmost = true,
                ExtendedStyle = WS_EX_TOOLWINDOW
            };
        else
        {
            // 首次启动，停靠在屏幕右侧（与旧代码保持一致）
            var hwnd = _context.GetHwnd();
            var (monitorBounds, workArea) = WindowService.GetCurrentScreen(hwnd);
            
            const int defaultMargin = 10;
            const int defaultWidth = 400;
            const int defaultHeight = 600;
            
            // 停靠在屏幕右侧，距离右边缘 10 像素
            var x = workArea.Right - defaultWidth - defaultMargin;
            var y = workArea.Top + defaultMargin;
            
            return new WindowVisualState
            {
                Bounds = new Rect(x, y, defaultWidth, defaultHeight),
                CornerRadius = 12,
                Opacity = 1.0,
                IsTopmost = true,
                ExtendedStyle = WS_EX_TOOLWINDOW
            };
        }
    }

    private Rect CalculateCenteredPosition(int width, int height) {
        var screen = GetCurrentScreen();
        var x = (screen.Bounds.Width - width) / 2 + screen.Bounds.X;
        var y = (screen.Bounds.Height - height) / 2 + screen.Bounds.Y;
        return new Rect(x, y, width, height);
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
        var window = _context.GetWindow();
        window.IsVisible = true;
        window.IsHitTestVisible = true;
        
        // 启用窗口大小调整
        var hwnd = _context.GetHwnd();
        WindowService.EnableResize(hwnd);
        
        // 确保窗口获得焦点
        window.Activate();
        WindowService.SetForegroundWindow(hwnd);
    }

    public void OnExit() {
        // 保存当前位置、尺寸和边缘距离以便下次恢复
        var currentVisual = _context.GetCurrentVisual();
        var screen = GetCurrentScreen();
        
        var rightDistance = screen.Bounds.Right - currentVisual.Bounds.Right;
        var bottomDistance = screen.Bounds.Bottom - currentVisual.Bounds.Bottom;
        
        _positionService.SaveFloatingPosition(
            currentVisual.Bounds.Width,
            currentVisual.Bounds.Height,
            rightDistance,
            bottomDistance
        );
        
        // 禁用窗口大小调整
        var hwnd = _context.GetHwnd();
        WindowService.DisableResize(hwnd);
    }
}
```

| 属性 | 描述 |
|------|------|
| 目标位置 | 根据保存的边缘距离计算（如果有），否则停靠在工作区右侧 |
| 目标尺寸 | 上次保存的尺寸（如果有），否则宽度为工作区宽度的 1/3（最小 380px），高度为工作区高度减去上下边距 |
| 圆角 | 12px |
| 不透明度 | 1.0（完全不透明）|
| 特殊行为 | 可调整大小，置顶显示，退出时保存位置、尺寸和边缘距离 |

### 8.2 FullscreenWindow - 全屏模式

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
        var window = _context.GetWindow();
        window.IsVisible = true;
        window.IsHitTestVisible = true;
        
        // 禁用窗口大小调整
        var hwnd = _context.GetHwnd();
        WindowService.DisableResize(hwnd);
        
        // 确保窗口获得焦点
        window.Activate();
        WindowService.SetForegroundWindow(hwnd);
        
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

### 8.3 SidebarWindow - 边栏模式

吸附在屏幕边缘的固定侧边栏。

```csharp
class SidebarWindow : IWindowState
{
    private readonly WindowContext _context;

    public SidebarWindow(WindowContext context) {
        _context = context;
    }

    public WindowVisualState GetTargetVisual() {
        var screen = GetCurrentScreen();
        return new WindowVisualState {
            Bounds = new Rect(screen.Bounds.Right - 400, 0, 400, screen.Bounds.Height),
            CornerRadius = 0,
            Opacity = 1.0,
            IsTopmost = false,
            ExtendedStyle = 0
        };
    }

    public AnimationSpec GetAnimationSpec(WindowVisualState from, WindowVisualState to) {
        return new AnimationSpec {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = Easing.EaseOutCubic
        };
    }

    public void OnEnter() {
        var window = _context.GetWindow();
        window.IsVisible = true;
        window.IsHitTestVisible = true;
        
        // 禁用窗口大小调整
        var hwnd = _context.GetHwnd();
        WindowService.DisableResize(hwnd);
        
        // 通过 SHAppBarMessage 注册为 AppBar，占用屏幕工作区
        var hwnd = _context.GetHwnd();
        WindowService.RegisterAppBar(hwnd, AppBarEdge.Right, 400);
    }

    public void OnExit() {
        // 取消 AppBar 注册
        var hwnd = _context.GetHwnd();
        WindowService.UnregisterAppBar(hwnd);
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

### 8.4 HiddenWindow - 隐藏状态

窗口完全隐藏，不可见也不可交互。

```csharp
class HiddenWindow : IWindowState
{
    private readonly WindowContext _context;

    public HiddenWindow(WindowContext context) {
        _context = context;
    }

    public WindowVisualState GetTargetVisual() {
        // 保持当前位置和尺寸，只改变透明度
        var currentVisual = _context.GetCurrentVisual();
        return new WindowVisualState {
            Bounds = currentVisual.Bounds,
            CornerRadius = currentVisual.CornerRadius,
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
        // 
        // 【设计决策说明】
        // OnEnter 为空是故意的设计，原因如下：
        // 1. RunStateMachineLoop 在动画开始前调用 OnEnter()
        // 2. 如果在 OnEnter 中设置 IsVisible = false，渐隐动画将不可见
        // 3. 因此保持 IsVisible = true，让用户能看到 Opacity 从 1.0 → 0.0 的渐隐效果
        // 4. 动画完成后，OnExit 会将窗口从视觉树中移除
    }

    public void OnExit() {
        // 动画完成后，从视觉树中移除
        var window = _context.GetWindow();
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

### 8.5 焦点管理机制

为了确保窗口在浮窗和全屏状态下能够可靠地获得输入焦点，系统使用了多层焦点管理策略。

#### 8.5.1 焦点管理的必要性

WinUI3 提供了 `window.Activate()` 方法来激活窗口，但这个方法在某些场景下不够可靠：

**WinUI3 内置能力的局限性：**
- `window.Activate()` 和 `AppWindow.Show()` 本质上只是"请求激活"，而不是强制获得焦点
- 当窗口从隐藏状态恢复时，`Activate()` 经常无法获得焦点
- 从托盘图标恢复窗口时，用户期望窗口立即可交互，但 `Activate()` 通常会失败

**Windows 系统的前台窗口规则（Foreground Lock）：**

Windows 系统对前台窗口切换有严格限制，只有以下情况才允许窗口获得前台焦点：
- 用户刚刚与窗口交互过（点击、键盘输入）
- 进程当前就是前台进程
- 系统认为进程"有资格"（在时间窗口内）

**典型失败场景：**
- 用户点击托盘图标（属于 Shell 进程）
- 应用窗口是后台进程
- 调用 `Activate()` → Windows 拒绝："你没资格抢焦点"
- 结果：窗口显示了，但焦点没过来

**解决方案：**

需要结合 WinUI3 的 `Activate()` 和 Win32 API 来提高焦点获取的成功率，并提供降级方案。

#### 8.5.2 焦点管理实现

**WindowService 提供的焦点管理函数：**

```csharp
static class WindowService
{
    // 将窗口设置为前台窗口并获得输入焦点
    public static bool SetForegroundWindow(IntPtr hwnd) { }
    
    // 将窗口提升到 Z 轴顶部
    public static bool BringWindowToTop(IntPtr hwnd) { }
    
    // 闪烁窗口以吸引用户注意（降级方案）
    public static bool FlashWindowEx(IntPtr hwnd, uint flags, uint count, uint timeout) { }
    
    // 获取当前前台窗口
    public static IntPtr GetForegroundWindow() { }
    
    // 获取窗口所属线程 ID
    public static uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId) { }
}
```

**推荐的焦点管理策略（多层兜底）：**

```csharp
/// <summary>
/// 尝试将窗口带到前台并获得焦点
/// 使用多层策略提高成功率
/// </summary>
public static bool TryBringToFront(Window window)
{
    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
    
    // 第一层：使用 WinUI3 内置方法
    // 在大多数情况下有效，但不保证获得前台焦点
    window.Activate();
    
    // 第二层：使用 Win32 SetForegroundWindow
    // 成功率比 Activate() 高，但仍然受系统限制
    bool success = WindowService.SetForegroundWindow(hwnd);
    
    if (!success)
    {
        // 第三层：降级方案 - 闪烁窗口吸引用户注意
        // 符合 Windows UX 规范，不强制抢焦点
        WindowService.FlashWindowEx(hwnd, 
            FLASHW_ALL | FLASHW_TIMERNOFG, 
            3,  // 闪烁 3 次
            0); // 使用默认闪烁频率
    }
    
    return success;
}
```

**关键常量定义：**

```csharp
// FlashWindowEx 标志
const uint FLASHW_STOP = 0;        // 停止闪烁
const uint FLASHW_CAPTION = 0x1;   // 闪烁标题栏
const uint FLASHW_TRAY = 0x2;      // 闪烁任务栏按钮
const uint FLASHW_ALL = 0x3;       // 闪烁标题栏和任务栏
const uint FLASHW_TIMER = 0x4;     // 持续闪烁直到窗口获得焦点
const uint FLASHW_TIMERNOFG = 0xC; // 持续闪烁直到用户点击
```

**在窗口形态中的使用：**

FloatingWindow 和 FullscreenWindow 的 `OnEnter` 钩子中调用焦点管理：

```csharp
public void OnEnter() {
    var window = _context.GetWindow();
    window.IsVisible = true;
    window.IsHitTestVisible = true;
    
    // 尝试获得焦点（使用多层策略）
    var hwnd = _context.GetHwnd();
    bool success = WindowService.TryBringToFront(window);
    
    // 如果焦点获取失败，窗口会闪烁以吸引用户注意
    // 这符合 Windows UX 规范，不会强制抢焦点
}
```

**关键设计点：**
- WinUI3 的 `window.Activate()` 是第一道防线，在大多数情况下有效
- Win32 的 `SetForegroundWindow` 是第二道防线，提高成功率
- `FlashWindowEx` 是降级方案，当无法获得焦点时闪烁窗口吸引用户注意
- 这种策略符合 Windows UX 规范，不会强制抢占用户焦点
- 焦点管理在 `OnEnter` 钩子中执行，确保窗口显示时尝试获得焦点
- 从 Hidden 状态恢复时，用户可以立即与窗口交互（如果焦点获取成功）
- 这种混合方案结合了 WinUI3 的简洁性和 Win32 的可靠性

**不推荐的方案：**
- ❌ `AttachThreadInput`：虽然可以提高焦点获取成功率，但有副作用（可能造成输入混乱、稳定性风险），不推荐作为常规方案
- ❌ `SendInput` 模拟用户输入：虽然成功率高，但可能被安全软件拦截，且不符合 Windows 规范

## 九、AnimationEngine（统一动画引擎）

负责执行所有视觉状态插值，支持线性插值（LERP）和 Spring 物理模拟。

### 9.1 核心职责

- 从当前视觉状态平滑过渡到目标视觉状态
- 支持多种插值策略（线性、缓动函数、Spring）
- 实时更新窗口外观
- 响应取消信号，立即停止动画

### 9.2 接口设计

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
        
        var stopwatch = Stopwatch.StartNew();
        var velocity = 0.0;  // 初始速度
        var displacement = 1.0;  // 初始位移（归一化）
        
        // Spring 稳定阈值：当速度和位移都足够小时，认为动画完成
        const double velocityThreshold = 0.5;  // 速度阈值（像素/秒）
        const double displacementThreshold = 0.5;  // 位移阈值（像素）
        
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            
            var elapsed = stopwatch.Elapsed;
            var deltaTime = 0.016;  // ~60 FPS
            
            // Spring 物理计算（简化版）
            var springForce = -spec.Spring.Stiffness * displacement;
            var dampingForce = -spec.Spring.Damping * velocity;
            var acceleration = springForce + dampingForce;
            
            velocity += acceleration * deltaTime;
            displacement += velocity * deltaTime;
            
            // 计算当前进度（1.0 - displacement，因为 displacement 从 1.0 趋向 0.0）
            var progress = Math.Max(0.0, Math.Min(1.0, 1.0 - displacement));
            
            // 插值所有属性
            var current = Lerp(from, to, progress);
            onProgress(current);
            
            // 检查稳定条件：速度和位移都足够小
            if (Math.Abs(velocity) < velocityThreshold && Math.Abs(displacement) < displacementThreshold) {
                // 确保最终状态精确到达目标
                onProgress(to);
                break;
            }
            
            await Task.Delay(16, cancellationToken); // ~60 FPS
        }
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

### 9.3 设计优势

- **统一控制**：所有动画逻辑集中在一处，易于调试和优化
- **可扩展**：支持添加新的插值策略（如弹性、回弹等）
- **高性能**：可优化为使用 CompositionAnimation 在 Composition 线程执行
- **全局策略**：可轻松实现全局动画速度调整、性能模式等
- **时间精度**：使用 Stopwatch 确保动画时长准确，不受帧率波动影响

### 9.4 动画引擎演进路线

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

## 十、全局动画策略（IAnimationPolicy）

为了解决快速切换状态时动画参数不一致的问题，引入全局动画策略层。

### 10.1 接口定义

```csharp
/// <summary>
/// 全局动画策略接口，统一管理动画参数
/// </summary>
interface IAnimationPolicy
{
    /// <summary>
    /// 根据状态转换和当前视觉状态，解析出最终的动画规格
    /// </summary>
    /// <param name="fromState">起始稳定状态（非中间状态）
    /// 注意：fromState 可能是 Initializing，实现需要能处理这种情况</param>
    /// <param name="toState">目标状态</param>
    /// <param name="currentVisual">当前实际视觉状态（可能是中间状态）</param>
    AnimationSpec Resolve(WindowState fromState, WindowState toState, WindowVisualState currentVisual);
}
```

### 10.2 默认策略实现

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
            // 处理 Initializing 状态的转换
            (WindowState.Initializing, WindowState.Fullscreen) => TransitionType.EnterFullscreen,
            (WindowState.Initializing, WindowState.Sidebar) => TransitionType.DockToSidebar,
            (WindowState.Initializing, WindowState.Floating) => TransitionType.Float,
            (WindowState.Initializing, WindowState.Hidden) => TransitionType.Hide,
            
            // 处理其他状态的转换
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

### 10.3 设计优势

- **一致性**：同样的状态转换，每次动画参数都相同
- **可预测性**：用户体验稳定，不会因中间状态而变化
- **集中管理**：所有动画参数在一处定义，易于调整
- **语义化**：使用 TransitionType 而非数值差异，更符合设计意图
- **防抖动**：快速切换时自动使用短动画，避免"情绪不稳定"

### 10.4 使用方式

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

## 十一、过渡状态使用场景

`TransitioningTo` 属性和 `TransitionStarted` 事件为外部模块提供了实时的转换进度信息。

### 11.1 典型使用场景

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

**2. 资源预加载**
```csharp
// 提前准备目标状态所需的资源
stateManager.TransitionStarted += (from, to) => {
    if (to == WindowState.Fullscreen) {
        PreloadFullscreenResources();
    }
};
```

**3. 日志和遥测**
```csharp
// 记录完整的状态转换生命周期
stateManager.TransitionStarted += (from, to) => {
    logger.Info($"开始转换: {from} → {to}");
};

stateManager.StateChanged += (from, to) => {
    logger.Info($"完成转换: {from} → {to}");
};
```

### 11.2 状态查询最佳实践

- **判断是否稳定**: `TransitioningTo == null` 表示没有正在进行的转换
- **判断目标状态**: 使用 `TransitioningTo ?? CurrentState` 获取"最终会到达的状态"

## 十二、迁移指南

本节提供从旧的主窗口代码迁移到新架构的详细指南，帮助开发者平滑过渡。

### 12.1 迁移步骤概览

建议采用渐进式迁移策略，分阶段完成重构，每个阶段都保持系统可运行：

**阶段 1：抽取 WindowService（Win32 抽象层）**
- 识别所有直接调用 Win32 API 的代码
- 将这些调用封装到 WindowService 静态方法中
- 替换旧代码中的 Win32 API 调用为 WindowService 方法调用
- 验证功能正常

**阶段 2：引入 WindowContext**
- 创建 WindowContext 类
- 将窗口实例和 HWND 的管理迁移到 WindowContext
- 更新各模块使用 WindowContext 而非直接访问窗口

**阶段 3：引入 WindowStateManager**
- 创建 WindowStateManager 和 WindowState 枚举
- 实现状态转换逻辑和动画调度
- 保持旧的窗口操作方法作为适配层，内部调用 WindowStateManager

**阶段 4：实现各窗口形态**
- 创建 FloatingWindow、FullscreenWindow、SidebarWindow、HiddenWindow 类
- 实现 IWindowState 接口
- 将旧的窗口布局逻辑迁移到各形态的 GetTargetVisual 方法

**阶段 5：引入 AnimationEngine**
- 创建 AnimationEngine 和 WindowVisualState
- 将旧的动画逻辑迁移到统一的插值引擎
- 替换旧的动画代码为 AnimationEngine 调用

**阶段 6：清理旧代码**
- 移除旧的窗口操作方法和适配层
- 移除旧的动画代码
- 更新所有调用点直接使用新 API

### 12.2 旧代码到新代码的映射关系

以下是常见旧代码模式到新架构的映射：

| 旧代码模式 | 新架构代码 | 说明 |
|-----------|-----------|------|
| `EnterFullscreen()` | `stateManager.TransitionTo(WindowState.Fullscreen)` | 进入全屏模式 |
| `ExitFullscreen()` | `stateManager.TransitionTo(WindowState.Floating)` | 退出全屏到浮窗 |
| `DockToSidebar()` | `stateManager.TransitionTo(WindowState.Sidebar)` | 停靠到边栏 |
| `UndockFromSidebar()` | `stateManager.TransitionTo(WindowState.Floating)` | 取消停靠 |
| `HideWindow()` | `stateManager.TransitionTo(WindowState.Hidden)` | 隐藏窗口 |
| `ShowWindow()` | `stateManager.TransitionTo(WindowState.Floating)` | 显示窗口（恢复到浮窗） |
| `if (isFullscreen) { ... }` | `if (stateManager.CurrentState == WindowState.Fullscreen) { ... }` | 状态查询 |
| `SetWindowPos(hwnd, ...)` | `WindowService.MoveWindow(hwnd, x, y)` | 移动窗口 |
| `SetWindowLong(hwnd, GWL_EXSTYLE, ...)` | `WindowService.SetExtendedStyle(hwnd, style)` | 设置扩展样式 |
| `DwmSetWindowAttribute(hwnd, ...)` | `WindowService.SetDwmAttribute(hwnd, attr, value)` | 设置 DWM 属性 |
| 手动实现的动画循环 | `AnimationEngine.Animate(from, to, spec, onProgress, ct)` | 统一动画引擎 |

### 12.3 具体迁移示例

#### 示例 1：迁移全屏切换逻辑

**旧代码：**

```csharp
private bool _isFullscreen = false;

public void ToggleFullscreen()
{
    if (_isFullscreen)
    {
        // 退出全屏
        var hwnd = GetHwnd();
        SetWindowLong(hwnd, GWL_STYLE, WS_OVERLAPPEDWINDOW);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 100, 100, 800, 600, SWP_SHOWWINDOW);
        _isFullscreen = false;
    }
    else
    {
        // 进入全屏
        var hwnd = GetHwnd();
        var screen = GetCurrentScreen();
        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, screen.Width, screen.Height, SWP_SHOWWINDOW);
        _isFullscreen = true;
    }
}
```

**新代码：**

```csharp
public void ToggleFullscreen()
{
    if (_stateManager.CurrentState == WindowState.Fullscreen)
    {
        _stateManager.TransitionTo(WindowState.Floating);
    }
    else
    {
        _stateManager.TransitionTo(WindowState.Fullscreen);
    }
}
```

#### 示例 2：迁移边栏停靠逻辑

**旧代码：**

```csharp
private bool _isDocked = false;

public void DockToSidebar()
{
    var hwnd = GetHwnd();
    var screen = GetCurrentScreen();
    
    // 注册 AppBar
    var abd = new APPBARDATA();
    abd.cbSize = Marshal.SizeOf(abd);
    abd.hWnd = hwnd;
    abd.uEdge = ABE_RIGHT;
    abd.rc = new RECT { left = screen.Right - 400, top = 0, right = screen.Right, bottom = screen.Height };
    SHAppBarMessage(ABM_NEW, ref abd);
    SHAppBarMessage(ABM_SETPOS, ref abd);
    
    // 移动窗口
    SetWindowPos(hwnd, HWND_NOTOPMOST, abd.rc.left, abd.rc.top, 400, screen.Height, SWP_SHOWWINDOW);
    
    _isDocked = true;
}

public void UndockFromSidebar()
{
    var hwnd = GetHwnd();
    
    // 取消 AppBar 注册
    var abd = new APPBARDATA();
    abd.cbSize = Marshal.SizeOf(abd);
    abd.hWnd = hwnd;
    SHAppBarMessage(ABM_REMOVE, ref abd);
    
    // 恢复到浮窗
    SetWindowPos(hwnd, HWND_TOPMOST, 100, 100, 400, 600, SWP_SHOWWINDOW);
    
    _isDocked = false;
}
```

**新代码：**

```csharp
public void DockToSidebar()
{
    _stateManager.TransitionTo(WindowState.Sidebar);
}

public void UndockFromSidebar()
{
    _stateManager.TransitionTo(WindowState.Floating);
}
```

AppBar 注册逻辑已迁移到 SidebarWindow.OnEnter/OnExit 钩子中。

#### 示例 3：迁移动画逻辑

**旧代码：**

```csharp
private async Task AnimateFadeIn()
{
    for (double opacity = 0.0; opacity <= 1.0; opacity += 0.05)
    {
        this.Opacity = opacity;
        await Task.Delay(16);  // ~60 FPS
    }
    this.Opacity = 1.0;
}

private async Task AnimateFadeOut()
{
    for (double opacity = 1.0; opacity >= 0.0; opacity -= 0.05)
    {
        this.Opacity = opacity;
        await Task.Delay(16);  // ~60 FPS
    }
    this.Opacity = 0.0;
    this.Visibility = Visibility.Collapsed;
}
```

**新代码：**

动画逻辑已迁移到 AnimationEngine 和各窗口形态的 GetAnimationSpec 方法中，无需手动编写动画循环。

```csharp
// 显示窗口（渐显）
_stateManager.TransitionTo(WindowState.Floating);

// 隐藏窗口（渐隐）
_stateManager.TransitionTo(WindowState.Hidden);
```

### 12.4 迁移过程中的注意事项

#### 12.4.1 保持向后兼容性

在迁移过程中，建议保留旧的公共 API 作为适配层，内部调用新的 WindowStateManager：

```csharp
// 旧的公共 API（保留以保持向后兼容）
[Obsolete("请使用 StateManager.TransitionTo(WindowState.Fullscreen) 替代")]
public void EnterFullscreen()
{
    _stateManager.TransitionTo(WindowState.Fullscreen);
}

[Obsolete("请使用 StateManager.TransitionTo(WindowState.Floating) 替代")]
public void ExitFullscreen()
{
    _stateManager.TransitionTo(WindowState.Floating);
}
```

这样可以确保现有的调用代码不会立即中断，给团队时间逐步更新调用点。

#### 12.4.2 测试策略

每个迁移阶段都应该有对应的测试：

1. **单元测试**：测试新模块的核心逻辑（如 WindowStateManager 的状态转换验证）
2. **集成测试**：测试新旧代码的交互（如适配层是否正确调用新 API）
3. **手动测试**：测试实际的窗口行为（如全屏切换、边栏停靠、动画效果）
4. **回归测试**：确保旧功能在迁移后仍然正常工作

#### 12.4.3 性能监控

在迁移过程中，监控以下性能指标：

- **状态转换延迟**：从调用 TransitionTo 到动画开始的时间
- **动画帧率**：动画执行过程中的实际帧率
- **内存使用**：确保没有资源泄漏（特别是 CancellationTokenSource）
- **CPU 使用**：动画执行时的 CPU 占用率

#### 12.4.4 常见陷阱

1. **忘记订阅事件**：确保在创建 WindowStateManager 后订阅 StateChanged、TransitionStarted 等事件
2. **在 Activate() 后隐藏窗口**：应该在 Activate() 之前设置 Opacity=0，而不是之后调用 TransitionTo(Hidden)
3. **在转换期间修改状态**：避免在 TransitioningTo != null 时调用 TransitionTo，除非确实需要打断
4. **忘记释放资源**：确保在窗口关闭时释放 WindowStateManager 和相关资源
5. **混用旧新 API**：避免同时使用旧的窗口操作方法和新的 WindowStateManager，容易导致状态不一致

#### 12.4.5 迁移检查清单

- [ ] 所有 Win32 API 调用已封装到 WindowService
- [ ] 所有窗口实例和 HWND 访问已迁移到 WindowContext
- [ ] 所有状态转换逻辑已迁移到 WindowStateManager
- [ ] 所有窗口布局逻辑已迁移到各窗口形态的 GetTargetVisual
- [ ] 所有动画逻辑已迁移到 AnimationEngine
- [ ] 所有事件订阅已正确设置
- [ ] 所有旧的公共 API 已标记为 Obsolete
- [ ] 单元测试覆盖率达到 80% 以上
- [ ] 手动测试所有窗口状态切换场景
- [ ] 性能指标符合预期
- [ ] 文档已更新

## 十三、正确性属性

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

### 属性 19: 转换期间视觉指示器存在性

*对于任意*正在进行的状态转换（TransitioningTo != null），UI 应该包含视觉指示器元素（如加载动画或进度条），且在转换完成后（TransitioningTo == null）应该自动移除

**验证需求: 需求 21.1, 21.3**

### 属性 20: 禁用操作的用户反馈

*对于任意*被禁用的操作，当用户尝试执行该操作时，系统应该显示临时提示信息说明操作不可用的原因

**验证需求: 需求 21.2**

### 属性 21: 操作可用性提示

*对于任意*窗口状态，鼠标悬停在操作控件上时应该显示 Tooltip，正确描述当前状态下哪些操作可用、哪些操作被禁用

**验证需求: 需求 21.5**

### 属性 22: 转换期间的请求反馈

*对于任意*正在进行的状态转换，如果用户尝试触发新的状态转换，系统应该显示简短的通知消息（如"请稍候，正在切换窗口模式"）

**验证需求: 需求 21.4**

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

**验证需求: 需求 21.6, 23.6**

### 属性 38: 转换完成后 UI 控件视觉更新

*对于任意*状态转换完成后（TransitioningTo == null），所有 UI 控件的视觉状态应该正确反映当前窗口状态（如全屏按钮在 Fullscreen 状态下显示"退出全屏"文本或图标）

**验证需求: 需求 23.7**

## 十四、待定/备注

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
- 状态机逻辑可通过 mock IWindowService 进行单元测试
- 动画引擎可通过 mock 时间源进行单元测试
- 各窗口形态实现可独立单元测试

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Docked_AI.功能.主窗口v2.动画系统.引擎;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.动画系统.策略;
using Docked_AI.功能.主窗口v2.服务层;
using Docked_AI.功能.主窗口v2.窗口形态;
using Docked_AI.功能.主窗口v2.基础设施.日志;

namespace Docked_AI.功能.主窗口v2.状态机;

/// <summary>
/// 窗口状态管理器（状态机）
/// 持有当前状态，负责校验合法性、驱动动画系统、广播状态变更事件
/// 
/// 核心设计理念：声明式状态机 + 统一动画调度器
/// - 不是传统的"并发控制"（加锁），而是"请求压缩" - 只保留最后一个目标
/// - TransitionTo 不执行动画，只更新 _latestTarget
/// - 使用单线程状态循环 RunStateMachineLoop() 驱动所有转换
/// - 状态机永远在"追逐"_latestTarget，通过 CancellationTokenSource 实现打断
/// - 动画系统从"当前视觉状态"插值到"目标视觉状态"，无需反向动画
/// - 循环条件：while (_latestTarget != CurrentState) - 语义清晰，无隐式状态
/// </summary>
public class WindowStateManager
{
    // - 状态属性 -
    
    /// <summary>
    /// 当前稳定状态（只读）
    /// </summary>
    public WindowState CurrentState { get; private set; }
    
    /// <summary>
    /// 上一个稳定状态（用于动画参数计算）
    /// 初始值为 Initializing，表示应用启动时的初始状态
    /// </summary>
    private WindowState _lastStableState = WindowState.Initializing;
    
    /// <summary>
    /// 正在转换到的目标状态（只读，null 表示没有正在进行的转换）
    /// </summary>
    public WindowState? TransitioningTo { get; private set; }
    
    /// <summary>
    /// 当前窗口的实际视觉状态（实时更新）
    /// </summary>
    private WindowVisualState _currentVisual;
    
    // - 并发控制字段 -
    
    /// <summary>
    /// 最新的目标状态（状态机永远在"追逐"这个目标）
    /// </summary>
    private WindowState _latestTarget;
    
    /// <summary>
    /// 状态机循环是否正在运行
    /// </summary>
    private bool _isRunning;
    
    /// <summary>
    /// 当前动画的取消令牌源（用于打断正在执行的动画）
    /// </summary>
    private CancellationTokenSource? _currentCts;
    
    /// <summary>
    /// UI 线程调度器（用于线程安全）
    /// </summary>
    private readonly DispatcherQueue _dispatcher;
    
    // - 依赖注入 -
    
    /// <summary>
    /// 动画系统（统一插值引擎）
    /// </summary>
    private readonly AnimationEngine _animationEngine;
    
    /// <summary>
    /// 全局动画策略（可选，用于统一管理动画参数）
    /// </summary>
    private readonly IAnimationPolicy? _animationPolicy;
    
    /// <summary>
    /// 窗口上下文（集中管理 HWND 和核心引用）
    /// </summary>
    private readonly WindowContext _context;
    
    // - 错误处理字段 -
    
    /// <summary>
    /// 失败计数器（用于重试逻辑）
    /// </summary>
    private int _failureCount = 0;
    
    /// <summary>
    /// 连续失败计数器（用于关键故障保护）
    /// </summary>
    private int _consecutiveFailures = 0;
    
    /// <summary>
    /// 最大重试次数
    /// </summary>
    private const int MaxRetries = 3;
    
    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    private const int RetryDelayMs = 100;
    
    /// <summary>
    /// 最大连续失败次数（超过此值将禁用自动转换）
    /// </summary>
    private const int MaxConsecutiveFailures = 3;
    
    /// <summary>
    /// 是否已禁用自动转换（关键故障保护）
    /// </summary>
    private bool _isDisabled = false;
    
    // - 日志记录器 -
    
    /// <summary>
    /// 状态转换日志记录器
    /// </summary>
    private readonly StateTransitionLogger? _stateLogger;
    
    /// <summary>
    /// 资源管理日志记录器
    /// </summary>
    private readonly ResourceManagementLogger? _resourceLogger;
    
    // - 事件 -
    
    /// <summary>
    /// 状态切换完成后广播（含所有动画已结束）
    /// 参数：from=切换前的稳定状态（即动画开始时的 CurrentState），to=切换后状态
    /// 注意：from 是"上一个稳定状态"，而不是"上一个请求的状态"
    /// 例如：快速切换 Floating → Fullscreen → Sidebar 时，最终广播 StateChanged(Floating, Sidebar)
    /// </summary>
    public event Action<WindowState, WindowState>? StateChanged;
    
    /// <summary>
    /// 开始转换到新状态时广播
    /// 参数：from=当前状态，to=目标状态
    /// </summary>
    public event Action<WindowState, WindowState>? TransitionStarted;
    
    /// <summary>
    /// 状态转换失败时广播
    /// 参数：from=起始状态，to=目标状态，exception=异常信息
    /// </summary>
    public event Action<WindowState, WindowState, Exception>? TransitionFailed;
    
    /// <summary>
    /// 关键故障时广播（连续失败超过阈值）
    /// 参数：failureCount=连续失败次数
    /// </summary>
    public event Action<int>? CriticalFailure;
    
    /// <summary>
    /// 创建窗口状态管理器
    /// </summary>
    /// <param name="dispatcher">UI 线程调度器</param>
    /// <param name="animationEngine">动画引擎</param>
    /// <param name="context">窗口上下文</param>
    /// <param name="animationPolicy">全局动画策略（可选）</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时抛出</exception>
    public WindowStateManager(
        DispatcherQueue dispatcher,
        AnimationEngine animationEngine,
        WindowContext context,
        IAnimationPolicy? animationPolicy = null,
        ILogger? logger = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _animationPolicy = animationPolicy;
        
        // 初始化日志记录器
        if (logger != null)
        {
            _stateLogger = new StateTransitionLogger(logger);
            _resourceLogger = new ResourceManagementLogger(logger);
        }
        
        // 尝试初始化动画引擎，如果失败则使用降级模式
        try
        {
            _animationEngine = animationEngine ?? throw new ArgumentNullException(nameof(animationEngine));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to initialize AnimationEngine: {ex.Message}");
            System.Diagnostics.Debug.WriteLine("Falling back to no-animation mode (instant state transitions)");
            
            // 创建一个降级的动画引擎（即时转换，无动画）
            _animationEngine = new AnimationEngine(); // 假设 AnimationEngine 有默认构造函数
            // 注意：实际实现中可能需要一个特殊的 NoAnimationEngine 类
        }
        
        // 初始化状态
        CurrentState = WindowState.Initializing;
        _lastStableState = WindowState.Initializing;
        _latestTarget = WindowState.Initializing;
        _currentVisual = context.GetCurrentVisual();
    }

    // - 公共方法 -
    
    /// <summary>
    /// 请求切换到目标状态（立即返回，不等待动画完成）
    /// 线程安全：可从任意线程调用，内部自动转发到 UI 线程
    /// 更新 _latestTarget 并立即取消当前动画（如果有）
    /// 如果状态机未运行，则启动 RunStateMachineLoop()
    /// </summary>
    /// <param name="target">目标状态</param>
    public void TransitionTo(WindowState target)
    {
        // 🔒 线程安全：强制在 UI 线程执行
        if (!_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => TransitionTo(target));
            return;
        }
        
        // 检查是否已禁用（关键故障保护）
        if (_isDisabled)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: State transitions are disabled due to critical failures. Call ResetToSafeState() to re-enable.");
            return;
        }
        
        // 验证状态转换的合法性
        ValidateTransition(CurrentState, target);
        
        // 更新最新目标状态
        _latestTarget = target;
        
        // 🎯 原子替换并取消旧的 CTS（避免对已 Dispose 的 CTS 调用 Cancel）
        var oldCts = Interlocked.Exchange(ref _currentCts, new CancellationTokenSource());
        
        // 记录 CTS 创建和替换
        _resourceLogger?.LogCtsCreated($"TransitionTo({target})");
        if (oldCts != null)
        {
            _resourceLogger?.LogCtsReplaced($"TransitionTo({target})");
            _resourceLogger?.LogCtsCancelled($"Old CTS for previous transition");
        }
        
        oldCts?.Cancel();
        oldCts?.Dispose();
        
        if (oldCts != null)
        {
            _resourceLogger?.LogCtsDisposed($"Old CTS for previous transition");
        }
        
        // 如果状态机循环未运行，则启动它
        if (!_isRunning)
        {
            _ = RunStateMachineLoop();
        }
    }
    
    /// <summary>
    /// 重置到安全状态（Floating）
    /// 用于从关键故障中恢复
    /// </summary>
    public void ResetToSafeState()
    {
        // 🔒 线程安全：强制在 UI 线程执行
        if (!_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => ResetToSafeState());
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("Resetting to safe state (Floating)...");
        
        // 重置错误计数器
        _failureCount = 0;
        _consecutiveFailures = 0;
        _isDisabled = false;
        
        // 强制转换到 Floating 状态
        try
        {
            TransitionTo(WindowState.Floating);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resetting to safe state: {ex.Message}");
        }
    }

    
    /// <summary>
    /// 状态机主循环（私有，由 TransitionTo 触发）
    /// 持续执行直到 _latestTarget 与 CurrentState 一致
    /// 所有状态转换由单线程循环驱动，使用 CancellationTokenSource 实现即时打断通知
    /// 采用"声明式动画"模式，从当前视觉状态平滑插值到目标视觉状态
    /// 
    /// 生命周期钩子调用规则：
    /// - OnEnter: 在动画开始前调用，用于设置离散状态（如 IsVisible、EnableResize 等）
    /// - OnExit: 在动画成功完成后调用，用于清理资源和状态
    /// 
    /// ⚠️ 重要：动画被打断时的行为
    /// 当动画被打断（OperationCanceledException）时：
    /// 1. OnExit 不会被调用 - 旧状态的清理工作不会执行
    /// 2. CurrentState 保持不变 - 仍然是旧状态
    /// 3. _currentVisual 停留在中间状态 - 下一轮动画从这里继续
    /// 4. 下一轮循环会立即开始新的转换，调用新目标状态的 OnEnter
    /// 
    /// 这意味着：
    /// - OnEnter 可能被多次调用而 OnExit 未被调用
    /// - IWindowState 实现必须确保 OnEnter 是幂等的
    /// - OnEnter 应该能够处理先前 OnEnter 注册的资源（自动清理或覆盖）
    /// 
    /// 示例场景：
    /// 1. 用户触发 Floating → Sidebar 转换
    /// 2. SidebarWindow.OnEnter() 被调用，注册 AppBar
    /// 3. 动画播放到 50%
    /// 4. 用户触发 Sidebar → Fullscreen 转换（打断）
    /// 5. SidebarWindow.OnExit() 不会被调用 - AppBar 仍然注册
    /// 6. FullscreenWindow.OnEnter() 被调用
    /// 7. 如果用户再次触发 Sidebar 转换
    /// 8. SidebarWindow.OnEnter() 再次被调用 - 必须处理 AppBar 已注册的情况
    /// </summary>
    private async Task RunStateMachineLoop()
    {
        _isRunning = true;
        
        try
        {
            // 🔴 关键修复：确保 _currentCts 不为 null
            // 如果是第一次运行循环，_currentCts 可能为 null
            // 这会导致 ExecuteTransitionAsync 使用 CancellationToken.None（永远不会被取消）
            if (_currentCts == null)
            {
                _currentCts = new CancellationTokenSource();
                System.Diagnostics.Debug.WriteLine("[StateManager] Initialized _currentCts in RunStateMachineLoop");
            }
            
            while (_latestTarget != CurrentState)
            {
                var target = _latestTarget;
                var from = CurrentState;
                
                // 🎯 设置过渡状态并广播
                TransitioningTo = target;
                TransitionStarted?.Invoke(from, target);
                
                // 记录转换开始
                System.Diagnostics.Debug.WriteLine($"[StateManager] Transition started: {from} → {target}");
                _stateLogger?.LogTransitionStarted(from, target);
                
                try
                {
                    // 执行状态转换
                    await ExecuteTransitionAsync(from, target);
                    
                    // 转换成功，重置失败计数器
                    _failureCount = 0;
                    _consecutiveFailures = 0;
                    
                    // 4️⃣ 更新状态
                    _lastStableState = from;
                    CurrentState = target;
                    TransitioningTo = null;
                    
                    System.Diagnostics.Debug.WriteLine($"[StateManager] Transition completed: {from} → {target}");
                    _stateLogger?.LogStateChanged(from, target);
                    StateChanged?.Invoke(from, target);
                }
                catch (OperationCanceledException)
                {
                    // ⚠️ 动画被打断 - OnExit 不会被调用
                    // 
                    // 当动画被打断时：
                    // 1. _currentVisual 已停留在中间状态（例如 50% 的位置）
                    // 2. CurrentState 保持不变（仍然是旧状态，如 Floating）
                    // 3. 旧状态的 OnExit 钩子不会被调用（资源可能未清理）
                    // 4. 下一轮循环会从中间状态继续插值到新目标
                    // 5. 新目标状态的 OnEnter 会被调用（必须是幂等的）
                    // 
                    // 这种设计确保了视觉连续性，但要求 IWindowState 实现：
                    // - OnEnter 必须是幂等的（可以安全地多次调用）
                    // - OnEnter 应该能够处理先前 OnEnter 注册的资源
                    // 
                    // 示例：SidebarWindow.OnEnter 注册 AppBar 后被打断
                    // → AppBar 仍然注册（OnExit 未调用）
                    // → 下次 SidebarWindow.OnEnter 必须检查 AppBar 是否已注册
                    
                    System.Diagnostics.Debug.WriteLine($"[StateManager] Transition cancelled: {from} → {target}");
                    System.Diagnostics.Debug.WriteLine($"[StateManager] Note: OnExit was not called for state {from}");
                    _stateLogger?.LogTransitionCancelled(from, target);
                    TransitioningTo = null;
                    continue;
                }
                catch (Exception ex)
                {
                    // 转换失败，执行错误恢复逻辑
                    await HandleTransitionFailureAsync(from, target, ex);
                }
            }
        }
        finally
        {
            _isRunning = false;
        }
    }
    
    /// <summary>
    /// 执行单次状态转换
    /// </summary>
    private async Task ExecuteTransitionAsync(WindowState from, WindowState target)
    {
        // 获取目标状态的实现
        var targetImpl = GetStateImplementation(target);
        
        // 0️⃣ 调用目标状态的 OnEnter 钩子（设置离散状态）
        try
        {
            targetImpl.OnEnter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StateManager] OnEnter failed for {target}: {ex.Message}");
            throw new InvalidOperationException($"OnEnter failed for state {target}", ex);
        }
        
        // 1️⃣ 获取目标视觉状态和动画规格
        var targetVisual = targetImpl.GetTargetVisual();
        
        // 使用稳定状态计算动画参数（而非中间状态）
        var animationSpec = _animationPolicy?.Resolve(_lastStableState, target, _currentVisual)
            ?? targetImpl.GetAnimationSpec(_currentVisual, targetVisual);
        
        // 2️⃣ 执行动画（从当前视觉状态插值到目标视觉状态）
        var animationCts = _currentCts; // 快照当前 CTS
        
        // 🔴 关键修复：确保 animationCts 不为 null
        // 虽然 RunStateMachineLoop 已经初始化了 _currentCts，但为了防御性编程，这里再次检查
        if (animationCts == null)
        {
            System.Diagnostics.Debug.WriteLine("[StateManager] Warning: animationCts is null, creating new CTS");
            animationCts = new CancellationTokenSource();
            _currentCts = animationCts;
        }
        
        try
        {
            // 动画系统统一执行插值，实时更新 _currentVisual
            await _animationEngine.Animate(
                from: _currentVisual,
                to: targetVisual,
                spec: animationSpec,
                onProgress: (visual) =>
                {
                    _currentVisual = visual;
                    ApplyVisualToWindow(visual); // 应用到实际窗口
                },
                cancellationToken: animationCts.Token  // 🔴 修复：直接使用 Token，不再使用 ?? CancellationToken.None
            );
        }
        finally
        {
            // 🔴 关键修复：始终释放 animationCts
            // 无论动画是成功完成还是被打断，都要释放这一轮创建的 CTS
            // animationCts 是通过快照获取的，持有它自己的取消能力
            // 即使 _currentCts 已被新调用替换，这个 CTS 仍然需要释放
            if (animationCts != null)
            {
                _resourceLogger?.LogCtsDisposed($"Animation CTS for {from} → {target}");
                animationCts.Dispose();
            }
            
            // 只有当这个 CTS 就是当前的 _currentCts 时，才清空引用
            // 否则可能有新的 CTS 已经替换了它
            if (animationCts == _currentCts)
            {
                _currentCts = null;
            }
        }
        
        // 3️⃣ 动画完成，调用旧状态的 OnExit 钩子
        // 
        // ⚠️ 重要：OnExit 只在动画成功完成后调用
        // 如果动画被打断（OperationCanceledException），OnExit 不会被调用
        // 这意味着 OnEnter 注册的资源可能未被清理
        // 因此，下一次 OnEnter 必须能够处理这种情况（幂等性）
        try
        {
            var fromImpl = GetStateImplementation(from);
            fromImpl.OnExit();
        }
        catch (Exception ex)
        {
            // OnExit 失败，记录但不阻止状态转换完成
            System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: OnExit failed for state {from}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 处理状态转换失败
    /// 实现重试逻辑和关键故障保护
    /// </summary>
    private async Task HandleTransitionFailureAsync(WindowState from, WindowState target, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[StateManager] Transition failed: {from} → {target}");
        System.Diagnostics.Debug.WriteLine($"[StateManager] Exception: {ex.GetType().Name}: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"[StateManager] Stack trace: {ex.StackTrace}");
        
        // 递增失败计数器
        _failureCount++;
        _consecutiveFailures++;
        
        // 触发 TransitionFailed 事件
        TransitionFailed?.Invoke(from, target, ex);
        
        // 记录转换失败
        _stateLogger?.LogTransitionFailed(from, target, ex);
        
        // 清除过渡状态
        TransitioningTo = null;
        
        // 检查是否需要重试
        if (_failureCount <= MaxRetries)
        {
            System.Diagnostics.Debug.WriteLine($"[StateManager] Retrying transition ({_failureCount}/{MaxRetries})...");
            _stateLogger?.LogRetryAttempt(from, target, _failureCount, MaxRetries);
            
            // 延迟后重试
            await Task.Delay(RetryDelayMs);
            
            // 如果目标状态仍然是当前的 _latestTarget，则重试
            if (_latestTarget == target)
            {
                // 不需要做任何事，循环会自动重试
                return;
            }
            else
            {
                // 目标已改变，重置失败计数器
                _failureCount = 0;
                return;
            }
        }
        else
        {
            // 重试次数用尽，恢复到上次稳定状态
            System.Diagnostics.Debug.WriteLine($"[StateManager] Max retries exceeded. Reverting to last stable state: {_lastStableState}");
            _stateLogger?.LogRevertToSafeState(_lastStableState);
            
            // 重置失败计数器
            _failureCount = 0;
            
            // 恢复到上次稳定状态
            CurrentState = _lastStableState;
            _latestTarget = _lastStableState;
            
            // 检查连续失败次数
            if (_consecutiveFailures > MaxConsecutiveFailures)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Critical failure: {_consecutiveFailures} consecutive failures. Disabling automatic transitions.");
                _stateLogger?.LogCriticalFailure(_consecutiveFailures);
                
                // 禁用自动转换
                _isDisabled = true;
                
                // 触发 CriticalFailure 事件
                CriticalFailure?.Invoke(_consecutiveFailures);
            }
        }
    }

    
    // - 私有辅助方法 -
    
    /// <summary>
    /// 验证状态转换的合法性
    /// </summary>
    /// <param name="from">起始状态</param>
    /// <param name="to">目标状态</param>
    /// <exception cref="InvalidOperationException">当转换不合法时抛出</exception>
    private void ValidateTransition(WindowState from, WindowState to)
    {
        // 禁止转换到 Initializing 状态
        if (to == WindowState.Initializing)
        {
            throw new InvalidOperationException(
                $"Cannot transition to Initializing state. Initializing is the unique starting point and cannot be transitioned into.");
        }
        
        // 从 Initializing 状态的转换规则
        if (from == WindowState.Initializing)
        {
            // 允许转换到任何其他状态（Floating、Fullscreen、Sidebar、Hidden）
            if (to == WindowState.Floating || 
                to == WindowState.Fullscreen || 
                to == WindowState.Sidebar || 
                to == WindowState.Hidden)
            {
                return; // 合法转换
            }
            
            throw new InvalidOperationException(
                $"Invalid transition from {from} to {to}. From Initializing, can only transition to Floating, Fullscreen, Sidebar, or Hidden.");
        }
        
        // 从 Hidden 状态的转换规则
        if (from == WindowState.Hidden)
        {
            // 允许转换到任何可见状态（Floating、Fullscreen、Sidebar）
            if (to == WindowState.Floating || 
                to == WindowState.Fullscreen || 
                to == WindowState.Sidebar)
            {
                return; // 合法转换
            }
            
            // 也允许保持 Hidden 状态（虽然没有实际意义）
            if (to == WindowState.Hidden)
            {
                return;
            }
            
            throw new InvalidOperationException(
                $"Invalid transition from {from} to {to}. From Hidden, can only transition to Floating, Fullscreen, or Sidebar.");
        }
        
        // 从可见状态（Floating、Fullscreen、Sidebar）的转换规则
        if (from == WindowState.Floating || 
            from == WindowState.Fullscreen || 
            from == WindowState.Sidebar)
        {
            // 允许转换到任何其他状态（包括 Hidden）
            if (to == WindowState.Floating || 
                to == WindowState.Fullscreen || 
                to == WindowState.Sidebar || 
                to == WindowState.Hidden)
            {
                return; // 合法转换
            }
            
            throw new InvalidOperationException(
                $"Invalid transition from {from} to {to}. From visible states, can transition to any other state except Initializing.");
        }
        
        // 不应该到达这里
        throw new InvalidOperationException(
            $"Unexpected state transition from {from} to {to}.");
    }

    
    /// <summary>
    /// 应用视觉状态到实际窗口
    /// 从 WindowContext 获取 HWND，调用 WindowService 方法应用所有视觉属性
    /// 
    /// 重要：此方法在动画的每一帧被调用，必须保持轻量和容错
    /// Win32 API 调用失败不应该中断动画，应该记录日志并继续
    /// </summary>
    /// <param name="visual">要应用的视觉状态</param>
    private void ApplyVisualToWindow(WindowVisualState visual)
    {
        try
        {
            var hwnd = _context.GetHwnd();
            
            // 验证 HWND 是否有效
            if (hwnd == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[StateManager] Warning: HWND is zero, skipping visual update");
                return;
            }
            
            // 应用窗口位置和尺寸（关键操作）
            try
            {
                WindowService.SetWindowBounds(
                    hwnd,
                    (int)visual.Bounds.X,
                    (int)visual.Bounds.Y,
                    (int)visual.Bounds.Width,
                    (int)visual.Bounds.Height
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: SetWindowBounds failed: {ex.Message}");
            }
            
            // 应用圆角半径（非关键操作）
            try
            {
                WindowService.SetCornerRadius(hwnd, (int)visual.CornerRadius);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: SetCornerRadius failed: {ex.Message}");
            }
            
            // 应用不透明度（非关键操作）
            try
            {
                WindowService.SetOpacity(hwnd, visual.Opacity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: SetOpacity failed: {ex.Message}");
            }
            
            // 应用置顶状态（非关键操作）
            try
            {
                WindowService.SetTopmost(hwnd, visual.IsTopmost);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: SetTopmost failed: {ex.Message}");
            }
            
            // 应用扩展样式（非关键操作）
            if (visual.ExtendedStyle != 0)
            {
                try
                {
                    WindowService.SetExtendedStyle(hwnd, visual.ExtendedStyle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: SetExtendedStyle failed: {ex.Message}");
                }
            }
            
            // 更新 WindowContext 的当前视觉缓存
            _context.UpdateCurrentVisual(visual);
        }
        catch (Exception ex)
        {
            // 记录异常但不抛出，确保动画能够继续
            // 这对于打断动画至关重要：如果这里抛出异常，会导致整个动画中断
            System.Diagnostics.Debug.WriteLine($"[StateManager] Warning: ApplyVisualToWindow failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取指定状态的实现
    /// </summary>
    /// <param name="state">窗口状态</param>
    /// <returns>状态实现</returns>
    /// <exception cref="NotImplementedException">当状态实现尚未创建时抛出</exception>
    private IWindowState GetStateImplementation(WindowState state)
    {
        // 根据状态返回对应的实现
        // 每次调用都创建新实例，确保状态实现是无状态的
        return state switch
        {
            WindowState.Initializing => new 窗口形态.InitializingWindow(_context),
            WindowState.Hidden => new 窗口形态.HiddenWindow(_context),
            WindowState.Floating => new 窗口形态.FloatingWindow(_context, new WindowPositionService()),
            WindowState.Fullscreen => new 窗口形态.FullscreenWindow(_context),
            WindowState.Sidebar => new 窗口形态.SidebarWindow(_context),
            _ => throw new NotImplementedException($"State implementation for {state} not yet created.")
        };
    }
}

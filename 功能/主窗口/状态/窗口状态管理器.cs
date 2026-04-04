using System;
using System.Collections.Generic;
using System.Linq;

namespace Docked_AI.Features.MainWindow.State;

/// <summary>
/// 窗口状态管理器，统一管理窗口状态转换
/// 
/// 核心设计：
/// 1. 命令模式状态机：返回 TransitionPlan 而不是直接执行副作用
/// 2. 视觉/逻辑状态分离：VisualState（UI 绑定）和 LogicalState（内部逻辑）
/// 3. 基于版本号的并发控制：使用 _transitionId 防止过期任务执行
/// 4. PendingState 机制：副作用成功后才提交状态，失败则自动回滚
/// 5. OS 同步事件排队：转换期间延迟外部同步事件
/// 6. 组合转换记录：记录子转换历史
/// 7. 线程安全：所有操作使用 lock 保护
/// </summary>
public sealed class WindowStateManager : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<StateTransition> _transitionHistory = new();
    private readonly List<CompositeTransition> _compositeTransitions = new();
    private readonly IDispatcher _dispatcher;
    private readonly Dictionary<WindowState, HashSet<WindowState>> _allowedTransitions;
    
    private const int MaxHistorySize = 100;
    private bool _isTransitioning = false;
    private bool _disposed = false;
    private int _transitionId = 0;
    private WindowState? _pendingSyncEvent = null;
    
    // PendingState 机制：防止状态漂移
    public WindowState CommittedState { get; private set; }
    public WindowState? PendingState { get; private set; }
    
    // 视觉/逻辑状态分离
    public WindowState VisualState => CommittedState; // UI 绑定到此状态
    public WindowState LogicalState => PendingState ?? CommittedState; // 内部逻辑使用
    
    // 当前状态：如果有 PendingState 则返回 PendingState，否则返回 CommittedState
    public WindowState CurrentState => LogicalState;
    
    public bool IsTransitioning
    {
        get { lock (_lock) { return _isTransitioning; } }
    }
    
    // 状态变化事件
    public event EventHandler<StateChangedEventArgs>? StateChanged;
    
    // 默认状态转换矩阵（支持直接转换：Pinned/Maximized -> Hidden）
    private static readonly Dictionary<WindowState, HashSet<WindowState>> _defaultAllowedTransitions = new()
    {
        // NotCreated 只能转换到 Windowed（首次显示必须经过窗口化状态）
        [WindowState.NotCreated] = new() { WindowState.Windowed },
        
        // Hidden 可以转换到 Windowed（标准显示）
        [WindowState.Hidden] = new() { WindowState.Windowed },
        
        // Windowed 可以转换到所有其他状态
        [WindowState.Windowed] = new() { WindowState.Hidden, WindowState.Maximized, WindowState.Pinned },
        
        // Maximized 可以还原到 Windowed，或直接隐藏（内部自动先还原再隐藏）
        [WindowState.Maximized] = new() { WindowState.Windowed, WindowState.Hidden },
        
        // Pinned 可以取消固定到 Windowed，或直接隐藏（内部自动先取消固定再隐藏）
        [WindowState.Pinned] = new() { WindowState.Windowed, WindowState.Hidden }
    };
    
    /// <summary>
    /// 构造函数，支持注入自定义转换矩阵（可选）
    /// </summary>
    public WindowStateManager(IDispatcher dispatcher, Dictionary<WindowState, HashSet<WindowState>>? customTransitions = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _allowedTransitions = customTransitions ?? _defaultAllowedTransitions;
        CommittedState = WindowState.NotCreated;
        PendingState = null;
        _transitionId = 0;
        _pendingSyncEvent = null;
    }
    
    /// <summary>
    /// 便捷工厂方法：从 UI 线程创建
    /// </summary>
    public static WindowStateManager CreateForUIThread()
    {
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue == null)
        {
            throw new InvalidOperationException("Must be called from UI thread");
        }
        return new WindowStateManager(new WinUIDispatcher(queue));
    }
    
    /// <summary>
    /// 检查是否可以转换到目标状态
    /// </summary>
    public bool CanTransitionTo(WindowState newState)
    {
        lock (_lock)
        {
            if (CommittedState == newState) return false;
            return _allowedTransitions.ContainsKey(CommittedState) && 
                   _allowedTransitions[CommittedState].Contains(newState);
        }
    }
    
    /// <summary>
    /// 创建状态转换执行计划（命令模式）
    /// StateManager 返回计划，Controller 执行计划
    /// 
    /// CRITICAL: TransitionId 用于防止竞态条件
    /// 场景：A→B (transitionId=1) 执行中，用户触发 B→C (transitionId=2)
    /// 结果：旧的 commit(1) 延迟到达时会被拒绝，避免覆盖正确状态
    /// </summary>
    public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
    {
        lock (_lock)
        {
            // 检查是否已释放
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WindowStateManager));
            }
            
            // 防重入：动画执行期间拒绝新的状态转换
            if (_isTransitioning)
            {
                System.Diagnostics.Debug.WriteLine($"Transition blocked: already transitioning to {CurrentState}");
                return null;
            }
            
            if (!CanTransitionTo(newState))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid transition: {CommittedState} -> {newState}");
                return null;
            }
            
            var previousState = CommittedState;
            var transitionId = ++_transitionId; // 捕获版本号
            
            // 关键：设置 PendingState，但不修改 CommittedState
            PendingState = newState;
            _isTransitioning = true; // 标记转换开始，阻止并发转换
            
            var transition = new StateTransition(previousState, newState, DateTime.Now, reason);
            
            // 使用固定大小的循环缓冲区
            _transitionHistory.Enqueue(transition);
            if (_transitionHistory.Count > MaxHistorySize)
            {
                _transitionHistory.Dequeue();
            }
            
            // 触发事件（在 UI 线程上）
            var handler = StateChanged;
            if (handler != null)
            {
                // 使用注入的 IDispatcher，确保事件在正确的线程触发
                // 捕获当前状态的快照，避免在异步执行时状态已改变
                var eventArgs = new StateChangedEventArgs(previousState, newState, DateTime.Now, transitionId, reason);
                
                bool enqueued = _dispatcher.TryEnqueue(() => 
                {
                    // 二次检查：Dispose 后不触发事件
                    lock (_lock)
                    {
                        if (_disposed) return;
                    }
                    
                    handler.Invoke(this, eventArgs);
                });
                
                if (!enqueued)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Failed to enqueue state change event for {previousState} -> {newState}");
                    
                    // 🔴 CRITICAL FIX: 调度失败时立即回滚并解锁
                    // 问题：TryEnqueue 失败 → 状态回滚，但 Controller 可能已执行部分 UI 副作用
                    // 解决：在 CreatePlan 内部立即回滚，Controller 收到 null 后不执行任何副作用
                    // 这确保了状态和 UI 的一致性：要么都成功，要么都不执行
                    PendingState = null;
                    _isTransitioning = false;
                    return null;
                }
            }
            
            // 创建执行计划（包含 transitionId 用于验证）
            var plan = new TransitionPlan(
                TransitionId: transitionId,
                From: previousState,
                To: newState,
                Execute: async () =>
                {
                    // 执行副作用（由 Controller 实现）
                    // Controller 会根据 From/To 状态执行相应的动画和样式变化
                    await System.Threading.Tasks.Task.CompletedTask;
                },
                Compensate: async () =>
                {
                    // 补偿操作（回滚副作用）
                    // 如果副作用失败，Controller 可以调用此函数恢复原状态
                    await System.Threading.Tasks.Task.CompletedTask;
                }
            );
            
            return plan;
        }
    }
    
    /// <summary>
    /// 提交状态转换，将 PendingState 提交为 CommittedState
    /// 副作用成功后调用此方法
    /// </summary>
    public void CommitTransition(int transitionId)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return; // 已释放，静默忽略
            }
            
            // 版本号验证：防止过期任务执行
            if (transitionId != _transitionId)
            {
                System.Diagnostics.Debug.WriteLine($"Commit rejected: stale transition (expected {_transitionId}, got {transitionId})");
                return;
            }
            
            if (PendingState.HasValue)
            {
                CommittedState = PendingState.Value;
                PendingState = null;
                System.Diagnostics.Debug.WriteLine($"Transition committed: {CommittedState}");
            }
            
            _isTransitioning = false;
            
            // 处理延迟的 OS 同步事件
            ProcessPendingSyncEvent();
        }
    }
    
    /// <summary>
    /// 回滚状态转换，清除 PendingState
    /// 副作用失败时调用此方法
    /// </summary>
    public void RollbackTransition(int transitionId, string? reason = null)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return; // 已释放，静默忽略
            }
            
            // 版本号验证：防止过期任务执行
            if (transitionId != _transitionId)
            {
                System.Diagnostics.Debug.WriteLine($"Rollback rejected: stale transition (expected {_transitionId}, got {transitionId})");
                return;
            }
            
            if (PendingState.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"Transition rolled back: {CommittedState} -> {PendingState.Value} (reason: {reason})");
                PendingState = null;
            }
            
            _isTransitioning = false;
            
            // 处理延迟的 OS 同步事件
            ProcessPendingSyncEvent();
        }
    }
    
    /// <summary>
    /// OS 同步事件排队：转换期间延迟外部同步事件
    /// </summary>
    public void QueueSyncEvent(WindowState osState)
    {
        lock (_lock)
        {
            if (_isTransitioning)
            {
                // 转换期间延迟同步，只保留最新的事件
                _pendingSyncEvent = osState;
                System.Diagnostics.Debug.WriteLine($"Sync event queued: {osState}");
            }
            else
            {
                // 立即同步
                SyncToOSState(osState);
            }
        }
    }
    
    /// <summary>
    /// 处理延迟的 OS 同步事件
    /// </summary>
    private void ProcessPendingSyncEvent()
    {
        if (_pendingSyncEvent.HasValue)
        {
            var osState = _pendingSyncEvent.Value;
            _pendingSyncEvent = null;
            System.Diagnostics.Debug.WriteLine($"Processing queued sync event: {osState}");
            SyncToOSState(osState);
        }
    }
    
    /// <summary>
    /// 同步到 OS 状态（内部方法，假设已在 lock 内）
    /// </summary>
    private void SyncToOSState(WindowState osState)
    {
        if (osState != CommittedState)
        {
            System.Diagnostics.Debug.WriteLine($"Syncing to OS state: {CommittedState} -> {osState}");
            // 触发状态转换（由 Controller 处理）
            // 注意：这里不直接调用 CreatePlan，而是通过事件通知 Controller
            // Controller 会调用 CreatePlan 并执行计划
        }
    }
    
    /// <summary>
    /// 记录子转换到组合转换历史
    /// </summary>
    public void RecordSubTransition(int transitionId, StateTransition subTransition)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            
            // 版本号验证：防止过期任务执行
            if (transitionId != _transitionId)
            {
                System.Diagnostics.Debug.WriteLine($"RecordSubTransition rejected: stale transition (expected {_transitionId}, got {transitionId})");
                return;
            }
            
            // 查找或创建当前的组合转换
            var currentComposite = _compositeTransitions.LastOrDefault();
            if (currentComposite == null || currentComposite.From != CommittedState)
            {
                // 创建新的组合转换
                currentComposite = new CompositeTransition(
                    From: CommittedState,
                    To: PendingState ?? CommittedState,
                    SubTransitions: new List<StateTransition>(),
                    Timestamp: DateTime.Now
                );
                _compositeTransitions.Add(currentComposite);
            }
            
            // 添加子转换
            currentComposite.SubTransitions.Add(subTransition);
            System.Diagnostics.Debug.WriteLine($"Sub-transition recorded: {subTransition.FromState} -> {subTransition.ToState}");
        }
    }
    
    /// <summary>
    /// 获取状态转换历史（返回副本，线程安全）
    /// </summary>
    public List<StateTransition> GetTransitionHistory()
    {
        lock (_lock)
        {
            return _transitionHistory.ToList();
        }
    }
    
    /// <summary>
    /// 获取组合转换历史（返回副本，线程安全）
    /// </summary>
    public List<CompositeTransition> GetCompositeTransitions()
    {
        lock (_lock)
        {
            return _compositeTransitions.ToList();
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            
            _disposed = true;
            StateChanged = null;
            _transitionHistory.Clear();
            _compositeTransitions.Clear();
            _isTransitioning = false;
            PendingState = null;
            _pendingSyncEvent = null;
        }
    }
}

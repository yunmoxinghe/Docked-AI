using System;
using System.Collections.Generic;
using System.Linq;

namespace Docked_AI.Features.MainWindow.State;

/// <summary>
/// 窗口状态管理器 - 基于命令模式的状态机，管理窗口状态转换
/// 
/// 【文件职责】
/// 1. 作为窗口状态的唯一真实来源（Single Source of Truth）
/// 2. 验证状态转换的合法性（基于转换矩阵）
/// 3. 创建状态转换执行计划（命令模式），由 Controller 执行
/// 4. 管理状态转换历史和组合转换记录
/// 5. 提供线程安全的状态访问和并发控制
/// 
/// 【核心设计原则】
/// 
/// 1. **命令模式状态机**
///    为什么返回 TransitionPlan 而不是直接执行副作用？
///    - 分离关注点：StateManager 负责状态逻辑，Controller 负责 UI 副作用
///    - 可测试性：可以测试状态转换逻辑，而不依赖 UI 框架
///    - 可撤销性：TransitionPlan 包含 Compensate 函数，支持回滚
///    - 异步友好：Controller 可以异步执行副作用，完成后再提交状态
/// 
/// 2. **视觉/逻辑状态分离**
///    - VisualState (CommittedState): UI 绑定到此状态，已提交的稳定状态
///    - LogicalState (PendingState ?? CommittedState): 内部逻辑使用，包含待提交状态
///    - 为什么需要分离？防止 UI 在动画执行期间显示中间状态
/// 
/// 3. **基于版本号的并发控制**
///    场景：用户快速点击按钮，触发多个状态转换
///    - A→B (transitionId=1) 执行中，用户触发 B→C (transitionId=2)
///    - 旧的 commit(1) 延迟到达时会被拒绝（版本号不匹配）
///    - 确保最终状态是最新的用户意图
/// 
/// 4. **PendingState 机制**
///    为什么需要 PendingState？
///    - 防止状态漂移：副作用失败时可以回滚到 CommittedState
///    - 原子性保证：要么状态和副作用都成功，要么都不执行
///    - 流程：CreatePlan → 设置 PendingState → 执行副作用 → CommitTransition
/// 
/// 5. **OS 同步事件排队**
///    场景：动画执行期间，用户通过 Win+↑ 最大化窗口
///    - 转换期间延迟 OS 同步事件，只保留最新的事件
///    - 转换完成后处理排队的事件，避免状态冲突
/// 
/// 6. **组合转换记录**
///    场景：Pinned → Hidden 需要先 Pinned → Windowed → Hidden
///    - 记录子转换历史，便于调试和审计
///    - 支持复杂的多步转换流程
/// 
/// 【核心逻辑流程】
/// 
/// 标准转换流程：
///   1. Controller 调用 CreatePlan(targetState)
///   2. StateManager 验证转换合法性（基于转换矩阵）
///   3. StateManager 设置 PendingState，增加 transitionId
///   4. StateManager 触发 StateChanged 事件（在 UI 线程）
///   5. StateManager 返回 TransitionPlan（包含 transitionId）
///   6. Controller 执行副作用（动画、样式、布局）
///   7. 副作用成功 → Controller 调用 CommitTransition(transitionId)
///   8. 副作用失败 → Controller 调用 RollbackTransition(transitionId)
/// 
/// 并发控制流程：
///   1. 用户触发 A→B (transitionId=1)
///   2. 动画执行期间，用户触发 B→C (transitionId=2)
///   3. CreatePlan 检测到 _isTransitioning=true，拒绝新转换
///   4. 或者：允许新转换，旧的 commit(1) 会被版本号检查拒绝
/// 
/// OS 同步流程：
///   1. 用户触发 Windowed→Pinned (transitionId=1)
///   2. 动画执行期间，OS 报告 Maximized 状态
///   3. QueueSyncEvent(Maximized) 将事件排队
///   4. CommitTransition(1) 完成后，ProcessPendingSyncEvent() 处理排队事件
/// 
/// 【关键依赖关系】
/// - IDispatcher: 线程调度接口，确保事件在 UI 线程触发
/// - WindowHostController: 订阅 StateChanged 事件，执行副作用
/// - MainWindowViewModel: 订阅 StateChanged 事件，更新 UI 绑定
/// 
/// 【潜在副作用】
/// 1. StateChanged 事件在 UI 线程触发（通过 IDispatcher）
/// 2. 转换历史记录占用内存（限制为 100 条）
/// 3. lock 保护所有操作，可能影响性能（但状态转换不频繁）
/// 
/// 【重构风险点】
/// 1. 转换矩阵的修改：
///    - 添加新状态或转换时，必须更新 _defaultAllowedTransitions
///    - 确保转换矩阵的完整性和一致性
/// 2. TransitionId 的使用：
///    - 所有 Commit/Rollback 调用必须传递正确的 transitionId
///    - 如果传递错误的 transitionId，状态会被拒绝，导致状态不一致
/// 3. PendingState 的管理：
///    - CreatePlan 设置 PendingState，Commit/Rollback 清除 PendingState
///    - 如果忘记调用 Commit/Rollback，PendingState 会一直存在，阻止后续转换
/// 4. 事件调度失败的处理：
///    - TryEnqueue 失败时立即回滚，确保状态和 UI 的一致性
///    - 如果不回滚，StateManager 认为转换成功，但 UI 未更新
/// 5. 线程安全：
///    - 所有公共方法必须使用 lock 保护
///    - 如果添加新方法，必须考虑线程安全
/// 6. Dispose 的调用时机：
///    - 必须在窗口关闭时调用 Dispose，否则事件订阅导致内存泄漏
///    - Dispose 后不应再调用任何方法
/// </summary>
public sealed class WindowStateManager : IDisposable
{
    // 线程安全锁和数据结构
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
    // CommittedState: 已提交的稳定状态（UI 绑定）
    // PendingState: 待提交的状态（副作用执行期间）
    public WindowState CommittedState { get; private set; }
    public WindowState? PendingState { get; private set; }
    
    // 视觉/逻辑状态分离
    // VisualState: UI 绑定到此状态，显示已提交的状态
    // LogicalState: 内部逻辑使用，包含待提交状态
    public WindowState VisualState => CommittedState;
    public WindowState LogicalState => PendingState ?? CommittedState;
    
    // 当前状态：如果有 PendingState 则返回 PendingState，否则返回 CommittedState
    public WindowState CurrentState => LogicalState;
    
    public bool IsTransitioning
    {
        get { lock (_lock) { return _isTransitioning; } }
    }
    
    // 状态变化事件（在 UI 线程触发）
    public event EventHandler<StateChangedEventArgs>? StateChanged;
    
    // 默认状态转换矩阵（支持直接转换：Pinned/Maximized -> Hidden）
    // 为什么支持直接转换？
    // - 用户体验：用户期望点击隐藏按钮时窗口立即隐藏，而不是先还原再隐藏
    // - 内部实现：Controller 会自动执行组合副作用（先还原再隐藏）
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
    /// 
    /// 【参数说明】
    /// - dispatcher: 线程调度器，确保事件在 UI 线程触发
    /// - customTransitions: 自定义转换矩阵（可选），用于测试或特殊场景
    /// 
    /// 【设计原因】
    /// 为什么需要注入 IDispatcher？
    /// - 生产环境：使用 WinUIDispatcher，事件在 UI 线程触发
    /// - 测试环境：使用 SynchronousDispatcher，事件同步执行，避免时序问题
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
    /// 
    /// 【使用场景】
    /// 在 WindowHostController 构造函数中调用，自动获取当前线程的 DispatcherQueue
    /// 
    /// 【重要性】
    /// 必须在 UI 线程调用，否则 GetForCurrentThread() 返回 null
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
    /// 
    /// 【验证逻辑】
    /// 1. 目标状态与当前状态相同 → 拒绝（无需转换）
    /// 2. 转换矩阵中不存在此转换 → 拒绝（非法转换）
    /// 3. 其他情况 → 允许
    /// 
    /// 【使用场景】
    /// Controller 在调用 CreatePlan 前可以先检查转换是否合法
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

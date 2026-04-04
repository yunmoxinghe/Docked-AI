# 窗口状态管理系统重构设计文档

## Overview

当前主窗口的状态管理系统使用两个布尔值（`IsWindowVisible` 和 `IsDockPinned`）来表示复杂的窗口状态，导致状态定义不清晰、转换逻辑分散在多个方法中。本次重构将引入明确的状态枚举和统一的状态管理器，通过状态机模式来管理窗口的五种状态（未创建、隐藏中、窗口化、最大化、已固定），确保状态转换的合法性和可追踪性。

**关键设计决策**:
1. **命令模式状态机（Command-Pattern State Machine）**: `StateManager` 返回 `TransitionPlan`（执行计划）而不是直接执行副作用，Controller 执行计划并根据结果提交或回滚状态。
2. **视觉状态与逻辑状态分离（Visual/Logical State Separation）**: 分离 `VisualState`（已提交，UI 绑定）和 `LogicalState`（待定，内部逻辑使用），只有副作用完成后才更新 `VisualState`。
3. **基于版本号的并发控制（Version-Based Concurrency Control）**: 使用递增的 `_transitionId` 跟踪转换，转换开始时捕获版本号，完成时验证版本号匹配，版本号不匹配则拒绝转换完成。
4. **组合转换记录（Composite Transition Recording）**: 显式拆分组合转换（如 `Pinned -> Windowed -> Hidden`），记录子转换，允许在子转换边界处中断。
5. **OS 同步事件排队（OS Synchronization Event Queuing）**: 转换期间延迟或忽略外部同步事件，使用外部事件队列，转换完成后处理最新的同步事件。
6. **状态提交策略（PendingState 机制）**: 引入 `PendingState` 和 `CommittedState`，副作用成功后才提交状态，失败则自动回滚，解决状态漂移问题。
7. **内部兜底解锁**: `WindowStateManager` 内部在 `TryEnqueue` 失败时立即解锁 `_isTransitioning`，防止状态机永久锁死。
8. **直接转换支持**: 允许 `Pinned -> Hidden` 和 `Maximized -> Hidden` 直接转换，内部自动执行组合副作用，简化状态图。
9. **线程安全增强**: `GetTransitionHistory()` 返回副本，防止集合修改异常。
10. **架构调整**: `WindowStateManager` 由 `WindowHostController` 持有，`MainWindowViewModel` 只订阅 `StateChanged` 事件，降低耦合。
11. **一次性迁移**: 不保留兼容层（`IsWindowVisible`/`IsDockPinned`），所有代码在重构时一次性迁移到新 API。

## Glossary

- **Bug_Condition (C)**: 当前系统无法明确识别窗口的实际状态（窗口化、最大化、隐藏中等），只能通过布尔值组合推断
- **Property (P)**: 重构后系统应使用明确的枚举类型表示状态，并通过统一的状态管理器处理所有状态转换
- **Preservation**: 所有现有的窗口行为（显示/隐藏动画、固定模式、最大化、自动隐藏等）必须保持不变
- **WindowState**: 表示窗口五种状态的枚举：NotCreated（未创建）、Hidden（隐藏中）、Windowed（窗口化）、Maximized（最大化）、Pinned（已固定）
- **WindowStateManager**: 统一的状态管理器，负责验证和执行所有状态转换，使用命令模式和版本号并发控制
- **TransitionPlan**: 状态转换执行计划，包含 Execute 和 Compensate 函数，由 StateManager 创建，由 Controller 执行
- **VisualState**: 视觉状态（已提交），UI 绑定到此状态，只有副作用完成后才更新
- **LogicalState**: 逻辑状态（待定），内部逻辑使用此状态，包含 PendingState
- **PendingState**: 待提交状态，副作用成功后才提交为 CommittedState，失败则自动回滚
- **CommittedState**: 已提交状态，表示副作用已成功执行的稳定状态
- **TransitionId**: 递增的版本号，用于跟踪和验证状态转换，防止过期任务执行
- **CompositeTransition**: 组合转换，由多个子转换组成（如 Pinned -> Windowed -> Hidden），记录子转换历史
- **SyncEventQueue**: OS 同步事件队列，转换期间延迟外部同步事件，转换完成后处理最新事件
- **MainWindowViewModel**: 当前的视图模型类，重构后只订阅 `StateChanged` 事件，不持有 `WindowStateManager`
- **WindowHostController**: 当前的窗口控制器类，重构后持有 `WindowStateManager`，负责协调状态转换和副作用执行
- **WindowLayoutState**: 当前的布局状态类，存储窗口的位置和尺寸信息

**关于需求 2.4 的澄清**:
需求 2.4 提到"将布局信息整合到统一的状态对象中"，这里的"整合"指的是通过 `WindowHostController` 协调 `WindowStateManager` 和 `WindowLayoutState` 的交互，而非将两者合并为一个类。具体实现：
- `WindowLayoutState` 保持独立，不感知 `WindowState` 枚举（单一职责原则）
- `WindowHostController` 在状态转换时同步更新布局信息（如固定模式下应用特定边界）
- 状态一致性通过事件驱动保证：`StateChanged` 事件触发布局更新
- 原子性通过 PendingState 机制保证：副作用成功后才提交状态，失败则回滚

这种设计避免了职责耦合，同时满足了"状态一致性和原子性"的目标。

**关于架构调整的说明**:
为了降低耦合度，`WindowStateManager` 的所有权从 `MainWindowViewModel` 移到 `WindowHostController`：
- **所有权**: `WindowStateManager` 由 `WindowHostController` 拥有和管理
- **创建时机**: 在 `WindowHostController` 构造函数中创建
- **释放时机**: 在 `WindowHostController.OnWindowClosed()` 中释放
- **访问方式**: `MainWindowViewModel` 通过订阅 `StateChanged` 事件获取状态变化通知
- **生命周期**: `WindowHostController` 和 `WindowStateManager` 的生命周期一致
- **职责分离**: 
  - `WindowHostController`: 持有 StateManager，执行副作用（动画、样式、AppBar）
  - `MainWindowViewModel`: 只订阅状态变化，更新 UI 绑定属性
  - `MainWindow.xaml.cs`: 只路由事件，不包含业务逻辑

## Bug Details

### Bug Condition

当前系统无法明确识别窗口的实际状态，状态转换逻辑分散且缺少验证机制。

**Formal Specification:**
```
FUNCTION isBugCondition(system)
  INPUT: system of type WindowManagementSystem
  OUTPUT: boolean
  
  RETURN (system.stateRepresentation == "two boolean flags")
         AND (system.stateTransitionLogic == "scattered across multiple methods")
         AND (system.stateTransitionValidation == "none")
         AND (system.stateHistory == "not tracked")
END FUNCTION
```

### Examples

- **示例 1**: 当窗口处于最大化状态时，系统只能通过 `IsWindowVisible=true` 和 `IsDockPinned=false` 来推断，无法直接识别"最大化"状态
  - 预期行为：系统应有 `CurrentState = WindowState.Maximized` 明确表示
  - 实际行为：需要检查 `AppWindow.Presenter.State` 才能确定是否最大化

- **示例 2**: 当用户尝试从隐藏状态直接切换到最大化状态时，系统没有验证机制阻止这种非法转换
  - 预期行为：状态管理器应拒绝非法转换并返回错误
  - 实际行为：可能导致未定义的行为或状态不一致

- **示例 3**: 当窗口从固定模式切换到窗口化模式时，转换逻辑分散在 `RestoreStandardDock` 方法中，难以追踪完整的状态变化
  - 预期行为：状态管理器应触发 `StateChanged` 事件，记录从 `Pinned` 到 `Windowed` 的转换
  - 实际行为：只是修改布尔值，没有事件通知或历史记录

- **边缘情况**: 当窗口在固定模式下被最大化时（理论上不应允许），系统缺少约束机制
  - 预期行为：状态转换矩阵应明确禁止 `Pinned -> Maximized` 转换
  - 实际行为：可能出现状态不一致

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- 用户点击托盘图标切换窗口显示/隐藏时的动画效果必须保持不变
- 用户点击固定按钮切换固定模式时的 AppBar 注册和窗口样式应用必须保持不变
- 用户最大化或还原窗口时的 Presenter 状态更新和 UI 图标变化必须保持不变
- 窗口失去焦点且未固定时的自动隐藏行为必须保持不变
- 窗口大小变化时的布局信息更新必须保持不变
- 窗口在固定模式下的 AppBar 消息处理和边界计算必须保持不变
- 窗口关闭时的资源清理必须保持不变

**Scope:**
所有不涉及状态表示和转换逻辑的代码应完全不受影响。这包括：
- 动画控制器（`SlideAnimationController`）的实现
- AppBar 注册和消息处理逻辑
- 窗口样式应用（`ApplyPinnedWindowStyle`、`RestoreStandardWindowStyle`）
- 布局计算和边界处理
- 背景效果服务（`BackdropService`）
- 标题栏服务（`TitleBarService`）

## Hypothesized Root Cause

基于 bug 描述和代码分析，主要问题包括：

1. **状态表示不足**: 使用两个布尔值（`IsWindowVisible` 和 `IsDockPinned`）无法准确表示五种窗口状态
   - 无法区分"窗口化"和"最大化"状态（两者都是 `IsWindowVisible=true, IsDockPinned=false`）
   - 需要额外检查 `AppWindow.Presenter.State` 才能确定是否最大化

2. **状态转换逻辑分散**: 状态转换代码散落在多个方法中
   - `ToggleWindow` 处理显示/隐藏切换
   - `TogglePinnedDock` 处理固定模式切换
   - `ShowWindow`、`HideWindow`、`ShowPinnedDock`、`RestoreStandardDock` 各自处理部分转换
   - 缺少统一的入口点和验证机制

3. **缺少状态转换验证**: 没有明确的状态转换规则
   - 可能出现非法的状态转换（如从隐藏直接到最大化）
   - 没有状态转换矩阵或状态机来约束转换

4. **状态同步问题**: 布局信息（`WindowLayoutState`）和 UI 状态（`MainWindowViewModel`）分离
   - 需要手动协调两个类之间的状态
   - 容易导致状态不一致

## Correctness Properties

Property 1: Bug Condition - 明确的状态表示和统一的状态转换

_For any_ 窗口状态变化操作，重构后的系统 SHALL 使用明确的 `WindowState` 枚举来表示当前状态，并通过统一的 `WindowStateManager` 来处理所有状态转换，在转换前验证转换的合法性，并在转换后触发 `StateChanged` 事件。

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

Property 2: Preservation - 现有窗口行为不变

_For any_ 用户交互或系统事件（托盘图标点击、固定按钮点击、窗口最大化、失去焦点等），重构后的系统 SHALL 产生与原系统完全相同的视觉效果和行为，保持所有动画、AppBar 注册、窗口样式、布局计算和资源清理逻辑不变。

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7**

## Fix Implementation

### Changes Required

假设我们的根因分析正确，需要进行以下更改：

**File 1**: `功能/主窗口/状态/窗口状态.cs` (新文件)

**Changes**:
1. **创建 WindowState 枚举**: 定义五种窗口状态
   ```csharp
   public enum WindowState
   {
       NotCreated,  // 窗口尚未创建
       Hidden,      // 窗口已隐藏
       Windowed,    // 窗口化模式（标准停靠）
       Maximized,   // 最大化模式
       Pinned       // 固定模式（AppBar）
   }
   ```

2. **创建 StateTransition 记录类型**: 用于追踪状态转换
   ```csharp
   public record StateTransition(
       WindowState FromState,
       WindowState ToState,
       DateTime Timestamp,
       string? Reason = null
   );
   ```

3. **创建 StateChangedEventArgs**: 用于状态变化事件
   ```csharp
   public class StateChangedEventArgs : EventArgs
   {
       public WindowState PreviousState { get; }
       public WindowState CurrentState { get; }
       public DateTime Timestamp { get; }
       public string? Reason { get; }
   }
   ```

**File 2**: `功能/主窗口/状态/窗口状态管理器.cs` (新文件)

**Changes**:
1. **创建 TransitionPlan 记录类型**: 命令模式执行计划（包含 TransitionId 防止竞态条件）
   ```csharp
   /// <summary>
   /// 状态转换执行计划（命令模式）
   /// StateManager 返回计划，Controller 执行计划
   /// 
   /// CRITICAL: TransitionId 用于防止竞态条件
   /// 场景：A→B (transitionId=1) 执行中，用户触发 B→C (transitionId=2)
   /// 结果：旧的 commit(1) 延迟到达时会被拒绝，避免覆盖正确状态
   /// </summary>
   public record TransitionPlan(
       int TransitionId,
       WindowState From,
       WindowState To,
       Func<Task> Execute,
       Func<Task>? Compensate = null
   );
   ```

2. **创建 CompositeTransition 记录类型**: 组合转换记录
   ```csharp
   /// <summary>
   /// 组合转换记录，包含子转换列表
   /// 用于记录 Pinned -> Windowed -> Hidden 等多步转换
   /// </summary>
   public record CompositeTransition(
       WindowState From,
       WindowState To,
       List<StateTransition> SubTransitions,
       DateTime Timestamp
   );
   ```

3. **创建 WindowStateManager 类**: 统一管理窗口状态（增强版）
   - 维护当前状态 `CurrentState`
   - 维护转换中标志 `_isTransitioning`（防止动画期间重入）
   - 维护释放标志 `_disposed`（防止释放后继续使用）
   - 使用抽象的 `IDispatcher` 接口（便于测试和线程调度）
   - 定义状态转换矩阵 `_allowedTransitions`（可通过构造函数注入自定义策略）
   - **新增：基于版本号的并发控制** `_transitionId`（防止过期任务执行）
   - **新增：视觉状态与逻辑状态分离** `VisualState` 和 `LogicalState`
   - **新增：OS 同步事件队列** `_pendingSyncEvent`（转换期间延迟外部同步）
   - **新增：组合转换记录** `_compositeTransitions`（记录子转换历史）
   - 提供状态转换方法 `CreatePlan(WindowState newState, string? reason = null)`（返回执行计划）
   - 提供状态验证方法 `CanTransitionTo(WindowState newState)`
   - 提供转换完成方法 `CommitTransition(int transitionId)`（验证版本号后提交）
   - 提供转换回滚方法 `RollbackTransition(int transitionId, string? reason = null)`（验证版本号后回滚）
   - 触发状态变化事件 `StateChanged`
   - 维护状态转换历史 `_transitionHistory`（使用固定大小的循环缓冲区，默认保留最近 100 条）
   - 实现 `IDisposable` 接口，确保事件订阅可以正确清理
   - 使用 `lock` 保证线程安全（所有状态读写操作都在锁内执行）

4. **创建 IDispatcher 接口**: 抽象线程调度，便于测试
   ```csharp
   public interface IDispatcher
   {
       bool TryEnqueue(Action callback);
   }
   
   // 生产环境实现
   internal sealed class WinUIDispatcher : IDispatcher
   {
       private readonly Microsoft.UI.Dispatching.DispatcherQueue _queue;
       
       public WinUIDispatcher(Microsoft.UI.Dispatching.DispatcherQueue queue)
       {
           _queue = queue ?? throw new ArgumentNullException(nameof(queue));
       }
       
       public bool TryEnqueue(Action callback)
       {
           return _queue.TryEnqueue(() => callback());
       }
   }
   
   // 测试环境实现（同步执行）
   internal sealed class SynchronousDispatcher : IDispatcher
   {
       public bool TryEnqueue(Action callback)
       {
           callback();
           return true;
       }
   }
   ```

2. **定义状态转换矩阵**: 明确允许的状态转换（支持直接转换以简化状态图）
   ```csharp
   // 设计决策：允许部分直接转换，内部自动执行组合副作用
   // 优势：状态图简单，用户体验流畅（一次点击完成复杂操作）
   // 实现：直接转换在内部分解为多步副作用，但对外表现为单次转换
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
   
   private readonly Dictionary<WindowState, HashSet<WindowState>> _allowedTransitions;
   
   // 构造函数支持注入自定义转换矩阵（可选）
   public WindowStateManager(IDispatcher dispatcher, Dictionary<WindowState, HashSet<WindowState>>? customTransitions = null)
   {
       _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
       _allowedTransitions = customTransitions ?? _defaultAllowedTransitions;
       CommittedState = WindowState.NotCreated;
       PendingState = null;
       _transitionId = 0;
       _pendingSyncEvent = null;
   }
   ```
   
   **设计理由**:
   - `NotCreated -> Windowed`: 窗口首次显示必须经过窗口化状态，确保有完整的初始化和入场动画
   - `Hidden -> Windowed`: 从隐藏状态恢复必须先显示为窗口化，不允许直接跳到 Pinned/Maximized
   - `Windowed` 作为中心状态: 可以转换到所有其他状态
   - `Maximized/Pinned -> Hidden`: 允许直接转换，内部自动执行"还原 + 隐藏"组合副作用
   - 优势：状态图简单（只有 7 条边），用户体验流畅（一次点击完成隐藏）
   - 实现复杂度：在 `ExecuteSideEffects` 方法中处理组合副作用

3. **实现状态转换验证**: 在转换前检查合法性
   ```csharp
   public bool CanTransitionTo(WindowState newState)
   {
       if (CurrentState == newState) return false;
       return _allowedTransitions[CurrentState].Contains(newState);
   }
   ```

4. **实现状态转换方法**: 使用命令模式 + 版本号并发控制 + 视觉/逻辑状态分离（线程安全 + 防重入 + 自动回滚 + 兜底解锁）
   
   **关键设计决策 - 命令模式状态机**:
   - `CreatePlan` 创建 `TransitionPlan`（执行计划），但不执行副作用
   - `TransitionPlan` 包含 `Execute` 和 `Compensate` 函数
   - Controller 执行计划，成功后调用 `CommitTransition(transitionId)` 提交状态
   - 失败时调用 `RollbackTransition(transitionId)` 回滚状态
   - 使用 `_transitionId` 版本号验证，防止过期任务执行
   
   **关键设计决策 - 视觉/逻辑状态分离**:
   - `VisualState`: 已提交状态，UI 绑定到此状态（等同于 `CommittedState`）
   - `LogicalState`: 逻辑状态，内部逻辑使用（等同于 `PendingState ?? CommittedState`）
   - 只有副作用完成后才更新 `VisualState`，避免 UI 提前变化
   
   **关键设计决策 - OS 同步事件排队**:
   - 转换期间延迟外部同步事件（`_pendingSyncEvent`）
   - 转换完成后处理最新的同步事件
   - 避免外部事件打断当前动画
   
   **失败处理策略**:
   - 副作用失败时自动回滚 `PendingState`，`CommittedState` 保持不变
   - 调度失败（`TryEnqueue` 返回 `false`）时，立即回滚并解锁状态机（兜底机制）
   - 测试环境使用 `SynchronousDispatcher`，副作用同步执行，避免时序差异
   
   ```csharp
   private readonly object _lock = new();
   private readonly Queue<StateTransition> _transitionHistory = new();
   private readonly List<CompositeTransition> _compositeTransitions = new();
   private const int MaxHistorySize = 100;
   private bool _isTransitioning = false;
   private bool _disposed = false;
   private readonly IDispatcher _dispatcher;
   private int _transitionId = 0; // 版本号，用于并发控制
   private WindowState? _pendingSyncEvent = null; // OS 同步事件队列
   
   // PendingState 机制：防止状态漂移
   public WindowState CommittedState { get; private set; }
   public WindowState? PendingState { get; private set; }
   
   // 视觉/逻辑状态分离
   public WindowState VisualState => CommittedState; // UI 绑定到此状态
   public WindowState LogicalState => PendingState ?? CommittedState; // 内部逻辑使用
   
   // 当前状态：如果有 PendingState 则返回 PendingState，否则返回 CommittedState
   public WindowState CurrentState => LogicalState;
   
   public WindowStateManager(IDispatcher dispatcher, Dictionary<WindowState, HashSet<WindowState>>? customTransitions = null)
   {
       _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
       _allowedTransitions = customTransitions ?? _defaultAllowedTransitions;
       CommittedState = WindowState.NotCreated;
       PendingState = null;
       _transitionId = 0;
       _pendingSyncEvent = null;
   }
   
   // 便捷工厂方法：从 UI 线程创建
   public static WindowStateManager CreateForUIThread()
   {
       var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
       if (queue == null)
       {
           throw new InvalidOperationException("Must be called from UI thread");
       }
       return new WindowStateManager(new WinUIDispatcher(queue));
   }
   
   public bool IsTransitioning
   {
       get { lock (_lock) { return _isTransitioning; } }
   }
   
   /// <summary>
   /// 创建状态转换执行计划（命令模式）
   /// StateManager 返回计划，Controller 执行计划
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
           
           // 检测是否为组合转换（需要多步副作用）
           bool isComposite = IsCompositeTransition(previousState, newState);
           
           // 创建执行计划（包含 transitionId 用于验证）
           var plan = new TransitionPlan(
               TransitionId: transitionId,
               From: previousState,
               To: newState,
               Execute: async () =>
               {
                   // 执行副作用（由 Controller 实现）
                   // Controller 会根据 From/To 状态执行相应的动画和样式变化
                   await Task.CompletedTask;
               },
               Compensate: async () =>
               {
                   // 补偿操作（回滚副作用）
                   // 如果副作用失败，Controller 可以调用此函数恢复原状态
                   await Task.CompletedTask;
               }
           );
           
           // 触发事件（在 UI 线程上）
           var handler = StateChanged;
           if (handler != null)
           {
               // 使用注入的 IDispatcher，确保事件在正确的线程触发
               // 捕获当前状态的快照，避免在异步执行时状态已改变
               var eventArgs = new StateChangedEventArgs(previousState, newState, DateTime.Now, reason);
               
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
           
           return plan;
       }
   }
   
   /// <summary>
   /// 检测是否为组合转换（需要多步副作用）
   /// </summary>
   private bool IsCompositeTransition(WindowState from, WindowState to)
   {
       return (from == WindowState.Maximized && to == WindowState.Hidden) ||
              (from == WindowState.Pinned && to == WindowState.Hidden);
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
   ```

**File 3**: `功能/主窗口/状态/主窗口视图模型.cs` (修改现有文件)

**Changes**:
1. **订阅 StateChanged 事件**: 不持有 StateManager，只订阅事件
   ```csharp
   // 不再持有 WindowStateManager，只订阅状态变化
   private WindowState _currentState = WindowState.NotCreated;
   
   public WindowState CurrentState
   {
       get => _currentState;
       private set
       {
           if (_currentState != value)
           {
               _currentState = value;
               RaisePropertyChanged(nameof(CurrentState));
           }
       }
   }
   
   // 由 WindowHostController 调用，订阅 StateManager 的事件
   public void SubscribeToStateManager(WindowStateManager stateManager)
   {
       if (stateManager == null)
       {
           throw new ArgumentNullException(nameof(stateManager));
       }
       
       stateManager.StateChanged += OnStateChanged;
       
       // 同步初始状态
       CurrentState = stateManager.CurrentState;
   }
   
   // 由 WindowHostController 调用，取消订阅
   public void UnsubscribeFromStateManager(WindowStateManager stateManager)
   {
       if (stateManager != null)
       {
           stateManager.StateChanged -= OnStateChanged;
       }
   }
   
   private void OnStateChanged(object? sender, StateChangedEventArgs args)
   {
       CurrentState = args.CurrentState;
   }
   
   // 不再需要 Dispose 方法释放 StateManager（不持有所有权）
   ```
   
   **所有权和生命周期说明**:
   - **所有权**: `WindowStateManager` 由 `WindowHostController` 拥有和管理
   - **创建时机**: 在 `WindowHostController` 构造函数中创建
   - **释放时机**: 在 `WindowHostController.OnWindowClosed()` 中释放
   - **访问方式**: `MainWindowViewModel` 通过订阅 `StateChanged` 事件获取状态变化通知
   - **生命周期保证**: `WindowHostController` 的生命周期必须 >= `MainWindowViewModel` 的生命周期
   - **释放顺序**: 
     1. `WindowHostController.OnWindowClosed` 中调用 `_viewModel.UnsubscribeFromStateManager(_stateManager)`
     2. `WindowHostController.OnWindowClosed` 中释放 `_stateManager.Dispose()`
   - **防御性编程**: 
     - `WindowStateManager.Dispose()` 是幂等的（多次调用安全）
     - `WindowStateManager` 在 `_disposed = true` 后拒绝所有操作（抛出 `ObjectDisposedException`）
     - `MainWindowViewModel` 在取消订阅后不再访问 `StateManager`

2. **移除兼容性属性**: 直接使用新的状态系统，不保留过渡期兼容层
   ```csharp
   // 不再提供 IsWindowVisible 和 IsDockPinned 属性
   // 所有调用方必须迁移到使用 CurrentState
   ```
   
   **设计理由**: 避免兼容层成为技术债务，强制所有代码在重构时一次性迁移到新 API
   
   **迁移指南**:
   
   **属性映射**:
   ```csharp
   // 旧代码 -> 新代码
   IsWindowVisible == true  =>  CurrentState != WindowState.Hidden && CurrentState != WindowState.NotCreated
   IsWindowVisible == false =>  CurrentState == WindowState.Hidden || CurrentState == WindowState.NotCreated
   IsDockPinned == true     =>  CurrentState == WindowState.Pinned
   IsDockPinned == false    =>  CurrentState != WindowState.Pinned
   ```

**File 4**: `功能/主窗口/显示隐藏/窗口宿主控制器.cs` (修改现有文件)

**Changes**:
1. **持有 WindowStateManager**: 拥有状态管理器的所有权
   ```csharp
   private readonly WindowStateManager _stateManager;
   
   public WindowHostController(Window window, MainWindowViewModel viewModel, int animationTimeoutMs = DefaultAnimationTimeoutMs)
   {
       // ... 初始化代码 ...
       
       _animationTimeoutMs = animationTimeoutMs;
       
       // 创建并持有 StateManager
       _stateManager = WindowStateManager.CreateForUIThread();
       _stateManager.StateChanged += OnWindowStateChanged;
       
       // ViewModel 订阅状态变化
       _viewModel.SubscribeToStateManager(_stateManager);
       
       _window.Closed += OnWindowClosed;
   }
   ```

2. **订阅状态变化事件**: 响应状态转换，执行副作用（使用命令模式 + 版本号验证 + 异常安全 + 超时保护 + 组合副作用）
   ```csharp
   // 动画超时配置（可通过构造函数注入或配置文件读取）
   private readonly int _animationTimeoutMs;
   private const int DefaultAnimationTimeoutMs = 2000;
   
   private async void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
   {
       // 获取执行计划（包含 transitionId）
       var plan = _stateManager.CreatePlan(args.CurrentState, args.Reason);
       if (plan == null)
       {
           System.Diagnostics.Debug.WriteLine($"Failed to create transition plan for {args.CurrentState}");
           return;
       }
       
       // 🟢 FIX: 从 plan 中获取 transitionId，而不是单独查询
       // 这确保了 transitionId 与 plan 的一致性，防止竞态条件
       int transitionId = plan.TransitionId;
       
       try
       {
           // 根据新状态执行相应的窗口操作（带超时保护）
           // 支持组合副作用：Maximized/Pinned -> Hidden 内部自动执行多步操作
           Task animationTask = (args.PreviousState, args.CurrentState) switch
           {
               // 简单转换
               (_, WindowState.Hidden) when args.PreviousState == WindowState.Windowed => ExecuteHideAnimationAsync(),
               (_, WindowState.Windowed) when args.PreviousState == WindowState.Hidden => ExecuteShowAnimationAsync(),
               (_, WindowState.Pinned) when args.PreviousState == WindowState.Windowed => ApplyPinnedModeAsync(),
               (_, WindowState.Maximized) when args.PreviousState == WindowState.Windowed => ApplyMaximizedModeAsync(),
               (_, WindowState.Windowed) when args.PreviousState == WindowState.Pinned => RestoreFromPinnedModeAsync(),
               (_, WindowState.Windowed) when args.PreviousState == WindowState.Maximized => RestoreFromMaximizedModeAsync(),
               
               // 组合副作用：Maximized -> Hidden（先还原再隐藏）
               (WindowState.Maximized, WindowState.Hidden) => ExecuteCompositeAsync(
                   transitionId,
                   new StateTransition(WindowState.Maximized, WindowState.Windowed, DateTime.Now, "Restore before hide"),
                   new StateTransition(WindowState.Windowed, WindowState.Hidden, DateTime.Now, "Hide after restore")
               ),
               
               // 组合副作用：Pinned -> Hidden（先取消固定再隐藏）
               (WindowState.Pinned, WindowState.Hidden) => ExecuteCompositeAsync(
                   transitionId,
                   new StateTransition(WindowState.Pinned, WindowState.Windowed, DateTime.Now, "Unpin before hide"),
                   new StateTransition(WindowState.Windowed, WindowState.Hidden, DateTime.Now, "Hide after unpin")
               ),
               
               _ => Task.CompletedTask
           };
           
           // 等待动画完成或超时
           var completedTask = await Task.WhenAny(animationTask, Task.Delay(_animationTimeoutMs));
           
           if (completedTask != animationTask)
           {
               System.Diagnostics.Debug.WriteLine($"WARNING: Animation timeout ({_animationTimeoutMs}ms) for state {args.CurrentState}");
               // 超时视为失败，回滚状态
               _stateManager.RollbackTransition(transitionId, "Animation timeout");
               
               // 执行补偿操作（如果有）
               if (plan.Compensate != null)
               {
                   await plan.Compensate();
               }
               return;
           }
           
           // 副作用成功，提交状态
           _stateManager.CommitTransition(transitionId);
       }
       catch (Exception ex)
       {
           // 记录动画执行异常，回滚状态
           System.Diagnostics.Debug.WriteLine($"Animation failed for state {args.CurrentState}: {ex.Message}");
           _stateManager.RollbackTransition(transitionId, $"Animation exception: {ex.Message}");
           
           // 执行补偿操作（如果有）
           if (plan.Compensate != null)
           {
               try
               {
                   await plan.Compensate();
               }
               catch (Exception compensateEx)
               {
                   System.Diagnostics.Debug.WriteLine($"Compensate failed: {compensateEx.Message}");
               }
           }
       }
   }
   
   /// <summary>
   /// 获取当前转换 ID（从 TransitionPlan 中获取，不需要单独方法）
   /// 注意：此方法已废弃，应使用 plan.TransitionId
   /// </summary>
   [Obsolete("Use plan.TransitionId instead")]
   private int GetCurrentTransitionId()
   {
       // 此方法已废弃，保留仅用于向后兼容
       // 新代码应直接使用 plan.TransitionId
       throw new NotImplementedException("Use plan.TransitionId instead");
   }
   
   /// <summary>
   /// 获取当前转换 ID（从 StateManager 内部状态读取）
   /// </summary>
   private int GetCurrentTransitionId()
   {
       // 注意：这需要 StateManager 暴露 CurrentTransitionId 属性
       // 或者在 CreatePlan 返回值中包含 transitionId
       // 这里假设 StateManager 提供了此属性
       return _stateManager.CurrentTransitionId;
   }
   
   /// <summary>
   /// 执行组合副作用（按顺序执行多个异步操作，记录子转换）
   /// </summary>
   private async Task ExecuteCompositeAsync(int transitionId, params StateTransition[] subTransitions)
   {
       foreach (var subTransition in subTransitions)
       {
           // 记录子转换到历史
           _stateManager.RecordSubTransition(transitionId, subTransition);
           
           // 执行子转换的副作用
           Task subTask = (subTransition.FromState, subTransition.ToState) switch
           {
               (WindowState.Maximized, WindowState.Windowed) => RestoreFromMaximizedModeAsync(),
               (WindowState.Pinned, WindowState.Windowed) => RestoreFromPinnedModeAsync(),
               (WindowState.Windowed, WindowState.Hidden) => ExecuteHideAnimationAsync(),
               _ => Task.CompletedTask
           };
           
           await subTask;
       }
   }
   
   private async Task ExecuteHideAnimationAsync()
   {
       // 执行隐藏动画
       _animationController.StartHide();
       await Task.Delay(300); // 等待动画完成
   }
   
   private async Task ExecuteShowAnimationAsync()
   {
       // 执行显示动画
       _animationController.StartShow();
       await Task.Delay(300); // 等待动画完成
   }
   
   private async Task ApplyPinnedModeAsync()
   {
       // 应用固定模式（可能包含动画）
       ApplyPinnedWindowStyle();
       ApplyPinnedBounds();
       _backdropService.EnsureMicaBackdrop(_window);
       await Task.Delay(100); // 等待样式应用完成
   }
   
   private async Task ApplyMaximizedModeAsync()
   {
       // 应用最大化模式
       if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           presenter.Maximize();
       }
       await Task.Delay(200); // 等待最大化动画完成
   }
   
   private async Task RestoreFromPinnedModeAsync()
   {
       // 从固定模式还原
       RestoreStandardWindowStyle();
       RemoveAppBar();
       await Task.Delay(100); // 等待样式还原完成
   }
   
   private async Task RestoreFromMaximizedModeAsync()
   {
       // 从最大化还原
       if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           presenter.Restore();
       }
       await Task.Delay(200); // 等待还原动画完成
   }
   
   private void OnWindowClosed(object sender, WindowEventArgs args)
   {
       // 清理事件订阅，避免内存泄漏
       _stateManager.StateChanged -= OnWindowStateChanged;
       
       // ViewModel 取消订阅
       _viewModel.UnsubscribeFromStateManager(_stateManager);
       
       // 释放 StateManager（拥有所有权）
       _stateManager.Dispose();
       
       RemoveAppBar();
   }
   ```

3. **重构状态转换方法**: 简化逻辑（使用命令模式 + 直接转换）
   ```csharp
   /// <summary>
   /// 切换窗口显示/隐藏状态
   /// 
   /// 行为说明：
   /// - 从 Hidden/NotCreated -> Windowed（显示窗口）
   /// - 从 Windowed -> Hidden（隐藏窗口）
   /// - 从 Maximized -> Hidden（直接转换，内部自动先还原再隐藏）
   /// - 从 Pinned -> Hidden（直接转换，内部自动先取消固定再隐藏）
   /// 
   /// UX 改进：
   /// 通过直接转换机制，Pinned/Maximized 状态下点击一次即可完成隐藏
   /// 状态图简单（只有 7 条边），用户体验流畅
   /// </summary>
   public void ToggleWindow()
   {
       EnsureWindowHandle();
       
       // 防重入：如果正在转换中，拒绝操作
       if (_stateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine("ToggleWindow blocked: transition in progress");
           return;
       }
       
       var currentState = _stateManager.CurrentState;
       
       if (currentState == WindowState.Hidden || currentState == WindowState.NotCreated)
       {
           var plan = _stateManager.CreatePlan(WindowState.Windowed, "User requested show");
           if (plan == null)
           {
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else
       {
           // 从任何可见状态直接隐藏（Windowed/Maximized/Pinned -> Hidden）
           var plan = _stateManager.CreatePlan(WindowState.Hidden, "User requested hide");
           if (plan == null)
           {
               System.Media.SystemSounds.Beep.Play();
           }
       }
   }
   
   public void TogglePinnedDock()
   {
       EnsureWindowHandle();
       
       // 防重入：如果正在转换中，拒绝操作
       if (_stateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine("TogglePinnedDock blocked: transition in progress");
           return;
       }
       
       var currentState = _stateManager.CurrentState;
       
       if (currentState == WindowState.Pinned)
       {
           var plan = _stateManager.CreatePlan(WindowState.Windowed, "User toggled pin off");
           if (plan == null)
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to unpin window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Windowed)
       {
           var plan = _stateManager.CreatePlan(WindowState.Pinned, "User toggled pin on");
           if (plan == null)
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to pin window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Maximized)
       {
           // 从最大化状态：先还原到 Windowed，用户需要再次点击固定
           var plan = _stateManager.CreatePlan(WindowState.Windowed, "Restore before pin");
           if (plan == null)
           {
               return;
           }
           System.Diagnostics.Debug.WriteLine("Restored, user needs to click pin again");
       }
       else
       {
           System.Diagnostics.Debug.WriteLine($"Cannot toggle pin from state: {currentState}");
       }
   }
   
   /// <summary>
   /// 切换最大化/还原状态
   /// 
   /// 行为说明：
   /// - 从 Windowed -> Maximized（最大化窗口）
   /// - 从 Maximized -> Windowed（还原窗口）
   /// - 从 Pinned -> Windowed（先取消固定，用户需要再次点击最大化）
   /// </summary>
   public void ToggleMaximize()
   {
       EnsureWindowHandle();
       
       // 防重入：如果正在转换中，拒绝操作
       if (_stateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine("ToggleMaximize blocked: transition in progress");
           return;
       }
       
       var currentState = _stateManager.CurrentState;
       
       if (currentState == WindowState.Maximized)
       {
           // 从最大化还原到窗口化
           var plan = _stateManager.CreatePlan(WindowState.Windowed, "User restored window");
           if (plan == null)
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to restore window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Windowed)
       {
           // 从窗口化最大化
           var plan = _stateManager.CreatePlan(WindowState.Maximized, "User maximized window");
           if (plan == null)
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to maximize window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Pinned)
       {
           // 从固定状态：先取消固定到 Windowed，用户需要再次点击最大化
           var plan = _stateManager.CreatePlan(WindowState.Windowed, "Unpin before maximize");
           if (plan == null)
           {
               return;
           }
           System.Diagnostics.Debug.WriteLine("Unpinned, user needs to click maximize again");
       }
       else
       {
           System.Diagnostics.Debug.WriteLine($"Cannot toggle maximize from state: {currentState}");
       }
   }
   
   /// <summary>
   /// 反向同步：从 OS 窗口状态同步到业务状态
   /// 场景：用户通过 Win+Up 快捷键最大化窗口，或通过任务栏按钮还原窗口
   /// 使用 OS 同步事件排队机制，转换期间延迟同步
   /// </summary>
   public void SyncFromOSWindowState()
   {
       if (_window.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           return;
       }
       
       var osState = presenter.State;
       var currentBusinessState = _stateManager.VisualState; // 使用 VisualState（已提交状态）
       
       // 映射 OS 状态到业务状态
       WindowState? targetState = osState switch
       {
           Microsoft.UI.Windowing.OverlappedPresenterState.Maximized => WindowState.Maximized,
           Microsoft.UI.Windowing.OverlappedPresenterState.Restored => WindowState.Windowed,
           _ => null
       };
       
       if (targetState == null || targetState == currentBusinessState)
       {
           return; // 状态一致，无需同步
       }
       
       // 使用 OS 同步事件排队机制
       // 如果正在转换中，事件会被延迟到转换完成后处理
       _stateManager.QueueSyncEvent(targetState.Value);
   }
   ```on(WindowState.Maximized, "Synced from OS");
           }
           else
           {
               System.Diagnostics.Debug.WriteLine($"Cannot sync to Maximized from {currentBusinessState}");
           }
       }
       else if (osState == Microsoft.UI.Windowing.OverlappedPresenterState.Restored && 
                currentBusinessState == WindowState.Maximized)
       {
           // OS 窗口已还原，但业务状态是 Maximized
           // 同步到 Windowed
           _stateManager.BeginTransition(WindowState.Windowed, "Synced from OS");
       }
   }
   ```onTo(WindowState.Windowed, "Restore before hide"))
               {
                   return;
               }
               // 设置延迟意图：动画完成后自动隐藏
               _pendingAction = PendingAction.HideAfterRestore;
               return;
           }
           
           // 从 Windowed 状态直接隐藏
           _viewModel.MarkHidden();
       }
   }
   
   /// <summary>
   /// 处理延迟意图（在 CompleteTransition 后调用）
   /// </summary>
   private void ProcessPendingAction()
   {
       var action = _pendingAction;
       _pendingAction = PendingAction.None; // 立即清除，避免重复执行
       
       // 安全卡口：如果正在转换中，不执行延迟意图（避免竞态）
       if (_viewModel.StateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine($"ProcessPendingAction blocked: transition in progress");
           return;
       }
       
       switch (action)
       {
           case PendingAction.HideAfterRestore:
               // 只有在 Windowed 状态下才执行隐藏
               if (_viewModel.CurrentState == WindowState.Windowed)
               {
                   _viewModel.MarkHidden();
               }
               break;
       }
   }
   ```
   
   **在 OnWindowStateChanged 的 finally 块中调用**:
   ```csharp
   finally
   {
       _viewModel.StateManager.CompleteTransition();
       
       // 处理延迟意图（在解锁后立即执行）
       // 注意：ProcessPendingAction 内部有防重入检查
       // 如果触发了新的 TransitionTo，事件会异步回到 OnWindowStateChanged
       // 建议在单测里专门跑一遍 Maximized -> Windowed -> Hidden 和 Pinned -> Windowed -> Hidden 链路
       ProcessPendingAction();
   }
   ```
   
   public void TogglePinnedDock()
   {
       EnsureWindowHandle();
       
       // 防重入：如果正在转换中，拒绝操作
       if (_viewModel.StateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine("TogglePinnedDock blocked: transition in progress");
           return;
       }
       
       var currentState = _viewModel.CurrentState;
       
       if (currentState == WindowState.Pinned)
       {
           if (!_viewModel.StateManager.TransitionTo(WindowState.Windowed, "User toggled pin off"))
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to unpin window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Windowed)
       {
           if (!_viewModel.StateManager.TransitionTo(WindowState.Pinned, "User toggled pin on"))
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to pin window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Maximized)
       {
           // 从最大化状态：先还原到 Windowed，并设置延迟固定意图
           if (!_viewModel.StateManager.TransitionTo(WindowState.Windowed, "Restore before pin"))
           {
               return;
           }
           // TODO: 如果需要支持 Maximized -> Windowed -> Pinned 链路，
           // 需要扩展 PendingAction 枚举添加 PinAfterRestore
           // 当前设计：用户需要点击两次（先还原，再固定）
           System.Diagnostics.Debug.WriteLine("Restored, user needs to click pin again");
       }
       else
       {
           System.Diagnostics.Debug.WriteLine($"Cannot toggle pin from state: {currentState}");
       }
   }
   
   /// <summary>
   /// 切换最大化/还原状态
   /// 
   /// 行为说明：
   /// - 从 Windowed -> Maximized（最大化窗口）
   /// - 从 Maximized -> Windowed（还原窗口）
   /// - 从 Pinned -> Windowed -> Maximized（先取消固定，动画完成后自动最大化）
   /// </summary>
   public void ToggleMaximize()
   {
       EnsureWindowHandle();
       
       // 防重入：如果正在转换中，拒绝操作
       if (_viewModel.StateManager.IsTransitioning)
       {
           System.Diagnostics.Debug.WriteLine("ToggleMaximize blocked: transition in progress");
           return;
       }
       
       var currentState = _viewModel.CurrentState;
       
       if (currentState == WindowState.Maximized)
       {
           // 从最大化还原到窗口化
           if (!_viewModel.StateManager.TransitionTo(WindowState.Windowed, "User restored window"))
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to restore window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Windowed)
       {
           // 从窗口化最大化
           if (!_viewModel.StateManager.TransitionTo(WindowState.Maximized, "User maximized window"))
           {
               System.Diagnostics.Debug.WriteLine("ERROR: Failed to maximize window");
               System.Media.SystemSounds.Beep.Play();
           }
       }
       else if (currentState == WindowState.Pinned)
       {
           // 从固定状态：先取消固定到 Windowed，并设置延迟最大化意图
           if (!_viewModel.StateManager.TransitionTo(WindowState.Windowed, "Unpin before maximize"))
           {
               return;
           }
           // TODO: 如果需要支持 Pinned -> Windowed -> Maximized 链路，
           // 需要扩展 PendingAction 枚举添加 MaximizeAfterRestore
           // 当前设计：用户需要点击两次（先取消固定，再最大化）
           System.Diagnostics.Debug.WriteLine("Unpinned, user needs to click maximize again");
       }
       else
       {
           System.Diagnostics.Debug.WriteLine($"Cannot toggle maximize from state: {currentState}");
       }
   }
   
   /// <summary>
   /// 反向同步：从 OS 窗口状态同步到业务状态
   /// 场景：用户通过 Win+Up 快捷键最大化窗口，或通过任务栏按钮还原窗口
   /// </summary>
   public void SyncFromOSWindowState()
   {
       if (_window.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           return;
       }
       
       var osState = presenter.State;
       var currentBusinessState = _viewModel.CurrentState;
       
       // 只在状态不一致时同步
       if (osState == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized && 
           currentBusinessState != WindowState.Maximized)
       {
           // OS 窗口已最大化，但业务状态不是 Maximized
           // 尝试同步到 Maximized
           if (currentBusinessState == WindowState.Windowed)
           {
               _viewModel.StateManager.TransitionTo(WindowState.Maximized, "Synced from OS");
           }
           else
           {
               System.Diagnostics.Debug.WriteLine($"Cannot sync to Maximized from {currentBusinessState}");
           }
       }
       else if (osState == Microsoft.UI.Windowing.OverlappedPresenterState.Restored && 
                currentBusinessState == WindowState.Maximized)
       {
           // OS 窗口已还原，但业务状态是 Maximized
           // 同步到 Windowed
           _viewModel.StateManager.TransitionTo(WindowState.Windowed, "Synced from OS");
       }
   }
   ```

**File 5**: `功能/主窗口/入口/主窗口.xaml.cs` (修改现有文件)

**Changes**:
1. **移除直接的 Presenter 操作**: 所有 maximize/restore 操作必须通过 `WindowStateManager` 执行
   ```csharp
   // 旧代码（直接操作 Presenter，绕过状态管理器）:
   private async void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
   {
       if (_viewModel.IsDockPinned)
       {
           TogglePinnedDock();
           await System.Threading.Tasks.Task.Delay(500);
       }
       ToggleWindowState();
   }
   
   public void ToggleWindowState()
   {
       if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
           {
               presenter.Restore();
           }
           else
           {
               presenter.Maximize();
           }
           UpdateWindowStateIcon();
       }
   }
   
   // 新代码（通过 WindowHostController 调用状态管理器）:
   private void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
   {
       _windowController.ToggleMaximize();
   }
   
   // ToggleWindowState() 方法删除，所有逻辑移到 WindowHostController
   ```

2. **移除 unpin-before-maximize 逻辑**: 此逻辑应由 `WindowHostController` 统一处理
   ```csharp
   // 旧代码（在 MainWindow 中处理 unpin-before-maximize）:
   private async void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
   {
       if (_viewModel.IsDockPinned)
       {
           TogglePinnedDock();
           await System.Threading.Tasks.Task.Delay(500);
       }
       ToggleWindowState();
   }
   
   // 新代码（逻辑移到 WindowHostController.ToggleMaximize）:
   private void OnWindowStateToggleRequested(object? sender, System.EventArgs e)
   {
       _windowController.ToggleMaximize();
   }
   ```

3. **移除 unpin-before-restore 逻辑**: 此逻辑应由 `WindowHostController` 统一处理
   ```csharp
   // 旧代码（在 MainWindow 中处理 restore-before-pin）:
   private async void OnDockToggleRequested(object? sender, System.EventArgs e)
   {
       if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
       {
           if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
           {
               presenter.Restore();
               UpdateWindowStateIcon();
               await System.Threading.Tasks.Task.Delay(500);
           }
       }
       TogglePinnedDock();
   }
   
   // 新代码（逻辑移到 WindowHostController.TogglePinnedDock）:
   private void OnDockToggleRequested(object? sender, System.EventArgs e)
   {
       _windowController.TogglePinnedDock();
   }
   ```

4. **保留 AppWindow.Changed 监听**: 用于反向同步 OS 窗口状态到业务状态
   ```csharp
   // 保留此监听器，但修改实现
   private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
   {
       if (args.DidPresenterChange || args.DidSizeChange)
       {
           // 反向同步：OS 窗口状态变化时，更新 WindowStateManager
           // 场景：用户通过 Win+Up 快捷键最大化窗口，或通过任务栏按钮还原窗口
           _windowController.SyncFromOSWindowState();
           
           UpdateWindowStateIcon();
           UpdateContentTopMargin();
       }
   }
   ```
   
   **设计理由**:
   - `AppWindow.Changed` 事件捕获 OS 级别的窗口状态变化（如用户通过快捷键或任务栏操作窗口）
   - 需要反向同步到 `WindowStateManager`，确保业务状态与 OS 状态一致
   - `SyncFromOSWindowState()` 方法在 `WindowHostController` 中实现，读取 `Presenter.State` 并调用 `TransitionTo`

5. **订阅 StateChanged 事件**: 响应状态变化更新 UI
   ```csharp
   public MainWindow()
   {
       InitializeComponent();
       
       _viewModel = new MainWindowViewModel();
       if (Content is FrameworkElement rootElement)
       {
           rootElement.DataContext = _viewModel;
       }
       
       _windowController = new WindowHostController(this, _viewModel);
       
       if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
       {
           linker.DockToggleRequested += OnDockToggleRequested;
           linker.WindowStateToggleRequested += OnWindowStateToggleRequested;
       }
       
       _viewModel.PropertyChanged += OnViewModelPropertyChanged;
       
       // 订阅状态变化事件，更新 UI
       _viewModel.StateManager.StateChanged += OnWindowStateChanged;
       
       // 监听 OS 窗口状态变化以反向同步
       this.AppWindow.Changed += OnAppWindowChanged;
   }
   
   private void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
   {
       // 根据新状态更新 UI（图标、边距等）
       UpdateWindowStateIcon();
       UpdateContentTopMargin();
       UpdateDockToggleIcon(args.CurrentState == WindowState.Pinned);
       UpdateContentCornerRadius(args.CurrentState == WindowState.Pinned);
   }
   ```

6. **实现 Dispose 或 Closed 事件处理**: 取消订阅事件
   ```csharp
   // 在构造函数中订阅 Closed 事件
   public MainWindow()
   {
       // ... 初始化代码 ...
       
       this.Closed += OnWindowClosed;
   }
   
   private void OnWindowClosed(object sender, WindowEventArgs args)
   {
       // 取消订阅，避免内存泄漏
       _viewModel.StateManager.StateChanged -= OnWindowStateChanged;
       this.AppWindow.Changed -= OnAppWindowChanged;
       
       if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Linker linker)
       {
           linker.DockToggleRequested -= OnDockToggleRequested;
           linker.WindowStateToggleRequested -= OnWindowStateToggleRequested;
       }
       
       _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
       
       // 释放 ViewModel（会级联释放 StateManager）
       _viewModel.Dispose();
   }
   ```

**File 6**: `功能/主窗口/定位尺寸/窗口布局状态.cs` (保持不变)

**Changes**:
1. **不修改此文件**: 保持布局状态类的单一职责，只负责位置和尺寸信息
   
2. **设计理由**: 
   - 布局状态不应感知窗口状态枚举，避免职责耦合
   - 状态相关的布局信息应由 `WindowStateManager` 或 `WindowHostController` 管理
   
3. **关于 WindowLayoutCache**: 
   - **本次重构不实现** `WindowLayoutCache` 类
   - 这是一个可选的后续优化，用于保存不同状态下的布局信息
   - 当前实现中，布局信息由 `WindowLayoutService` 动态计算即可
   - 如果未来需要记忆用户在不同状态下的窗口尺寸偏好，可以考虑实现此类：
   ```csharp
   // 示例代码（暂不实现）
   internal sealed class WindowLayoutCache
   {
       private readonly Dictionary<WindowState, (int Width, int Height, int X, int Y)> _layouts = new();
       
       public void SaveLayout(WindowState state, int width, int height, int x, int y)
       {
           _layouts[state] = (width, height, x, y);
       }
       
       public bool TryGetLayout(WindowState state, out int width, out int height, out int x, out int y)
       {
           if (_layouts.TryGetValue(state, out var layout))
           {
               (width, height, x, y) = layout;
               return true;
           }
           width = height = x = y = 0;
           return false;
       }
   }
   ```

## Testing Strategy

### Validation Approach

测试策略分为两个阶段：首先在未修改的代码上运行探索性测试，观察当前系统的行为和问题；然后在重构后的代码上运行修复验证测试和保留性测试，确保问题已解决且现有行为未改变。

### Exploratory Bug Condition Checking

**Goal**: 在实施修复前，通过手动测试或代码审查确认当前系统的问题。由于问题已在需求文档中明确描述，不需要编写专门的探索性测试代码。

**Verification Approach**: 
1. **代码审查**: 检查 `MainWindowViewModel` 和 `WindowHostController` 的代码，确认状态表示使用布尔值、转换逻辑分散在多个方法中
2. **手动测试**: 在当前系统上执行以下操作，观察问题：
   - 最大化窗口后检查 `IsWindowVisible` 和 `IsDockPinned`（无法区分窗口化和最大化）
   - 尝试快速切换状态（可能出现状态不一致）
   - 检查是否有状态转换历史记录（没有）

**Expected Observations**:
- 无法通过布尔值准确识别窗口的实际状态
- 状态转换逻辑分散在多个方法中，难以追踪
- 没有状态转换验证机制
- 没有状态转换历史记录

### Fix Checking

**Goal**: 验证重构后的系统对所有状态转换场景都能正确处理。

**Pseudocode:**
```
// 注：isValidTransition(transition) 定义为：
// 满足 _allowedTransitions 矩阵中定义的转换路径
// 即 _allowedTransitions[transition.fromState].Contains(transition.toState) == true

FOR ALL stateTransition WHERE isValidTransition(stateTransition) DO
  result := stateManager.TransitionTo(stateTransition.toState)
  ASSERT result == true
  ASSERT stateManager.CurrentState == stateTransition.toState
  ASSERT stateChangedEvent was triggered
  ASSERT stateManager.IsTransitioning == true (before CompleteTransition)
  stateManager.CompleteTransition()
  ASSERT stateManager.IsTransitioning == false (after CompleteTransition)
END FOR

FOR ALL stateTransition WHERE NOT isValidTransition(stateTransition) DO
  result := stateManager.TransitionTo(stateTransition.toState)
  ASSERT result == false
  ASSERT stateManager.CurrentState == stateTransition.fromState (unchanged)
  ASSERT stateChangedEvent was NOT triggered
  ASSERT stateManager.IsTransitioning == false (no transition started)
END FOR
```

### Preservation Checking

**Goal**: 验证重构后所有现有的窗口行为保持不变。

**Pseudocode:**
```
FOR ALL userInteraction IN [clickTrayIcon, clickPinButton, maximizeWindow, lostFocus, resizeWindow, closeWindow] DO
  originalBehavior := observeBehaviorOnOriginalCode(userInteraction)
  refactoredBehavior := observeBehaviorOnRefactoredCode(userInteraction)
  ASSERT originalBehavior == refactoredBehavior
END FOR
```

**Testing Approach**: 基于属性的测试（Property-Based Testing）推荐用于保留性检查，因为：
- 自动生成大量测试用例覆盖输入域
- 捕获手动单元测试可能遗漏的边缘情况
- 提供强有力的保证，确保所有非 bug 输入的行为不变

**Test Plan**: 首先在未修改的代码上观察各种用户交互的行为，然后编写基于属性的测试捕获这些行为，在重构后的代码上验证行为一致性。

**Test Cases**:
1. **显示/隐藏动画保留**: 观察托盘图标点击后的动画效果，验证重构后动画完全相同
2. **固定模式保留**: 观察固定按钮点击后的 AppBar 注册和窗口样式，验证重构后行为一致
3. **最大化行为保留**: 观察窗口最大化和还原的 Presenter 状态变化，验证重构后一致
4. **自动隐藏保留**: 观察窗口失去焦点时的自动隐藏行为，验证重构后一致
5. **布局更新保留**: 观察窗口大小变化时的布局信息更新，验证重构后一致
6. **AppBar 消息保留**: 观察固定模式下的 AppBar 消息处理，验证重构后一致
7. **资源清理保留**: 观察窗口关闭时的资源清理，验证重构后一致

### Unit Tests

- 测试 `WindowStateManager` 的状态转换验证逻辑
- 测试状态转换矩阵的正确性（允许的转换和禁止的转换）
- 测试 PendingState 机制（CreatePlan、CommitTransition、RollbackTransition）
- **新增：测试命令模式执行计划**（TransitionPlan 的创建和执行）
- **新增：测试版本号并发控制**（过期任务被正确拒绝）
- **🔴 CRITICAL: 测试 TransitionPlan.TransitionId 防止竞态条件**
  - 快速连续调用 CreatePlan，验证每个 plan 有唯一的 TransitionId
  - 模拟延迟提交，验证过期的 transitionId 被正确拒绝
  - 验证 plan.TransitionId 与 _transitionId 的一致性
- **🟠 CRITICAL: 测试 TryEnqueue 失败时的状态回滚**
  - Mock IDispatcher.TryEnqueue 返回 false
  - 验证 CreatePlan 返回 null
  - 验证 PendingState 被清除，_isTransitioning 被解锁
  - 验证 Controller 不会执行任何副作用
- **新增：测试视觉/逻辑状态分离**（VisualState 和 LogicalState 的正确性）
- **新增：测试 OS 同步事件排队**（转换期间延迟同步，转换完成后处理）
- **新增：测试组合转换记录**（子转换正确记录到历史）
- 测试状态变化事件的触发（通过 mock `IDispatcher` 验证事件调度）
- 测试状态转换历史的记录（包括循环缓冲区的容量限制）
- 测试 `GetTransitionHistory` 和 `GetCompositeTransitions` 返回副本（线程安全）
- 测试 `Dispose` 方法正确清理事件订阅和历史记录
- 测试 `Dispose` 后调用 `CreatePlan` 抛出 `ObjectDisposedException`
- 测试线程安全（多线程并发调用 `CreatePlan` 不会导致状态不一致）
- 测试防重入机制（`_isTransitioning` 标志在转换期间阻止新的转换请求）
- 测试 `TryEnqueue` 失败时的兜底解锁机制
- 测试动画期间快速点击不会触发多次状态转换
- 测试组合副作用的正确执行（Pinned -> Hidden, Maximized -> Hidden）
- 测试超时保护机制（超时后正确调用 `RollbackTransition`）
- 测试异常处理（副作用抛出异常时正确回滚）
- 测试补偿操作（Compensate 函数在回滚时正确执行）
- 测试 `MainWindowViewModel` 订阅和取消订阅机制
- 测试 `WindowHostController` 的所有权和生命周期管理

### Property-Based Tests

- 生成随机的状态转换序列，验证所有合法转换都能成功执行
- 生成随机的非法转换尝试，验证都被正确拒绝
- 生成随机的用户交互序列，验证重构前后的窗口行为完全一致
- 测试状态转换的幂等性（相同状态转换到相同状态应返回 null）
- 测试状态转换的原子性（转换失败时状态不变，PendingState 被清除）
- 测试多线程场景：多个线程并发调用 `CreatePlan`，验证状态转换的顺序性和一致性
- 测试事件订阅和取消订阅的正确性（订阅后能收到事件，取消订阅后不再收到）
- 测试防重入场景：在动画执行期间快速触发多次状态转换请求，验证只有第一次成功，后续请求被正确拒绝
- 测试 `CommitTransition` 和 `RollbackTransition` 调用后，新的状态转换请求能够正常执行
- 测试组合副作用的各种组合（不同的起始状态和目标状态）
- 测试超时和异常场景：随机注入超时和异常，验证状态机能正确回滚
- **🔴 CRITICAL: 测试版本号并发控制（竞态条件防护）**
  - 随机生成过期的 transitionId，验证 CommitTransition 和 RollbackTransition 正确拒绝
  - 模拟快速连续点击（A→B→C），验证中间状态的 commit 被正确拒绝
  - 验证最终状态与最后一次转换一致，不会被过期 commit 覆盖
- **🟠 CRITICAL: 测试 TryEnqueue 失败场景（状态/UI 一致性）**
  - 随机注入 TryEnqueue 失败，验证 CreatePlan 返回 null
  - 验证 Controller 不执行任何副作用
  - 验证状态机正确解锁，后续转换能正常执行
- **新增：测试 OS 同步事件排队**：随机生成 OS 同步事件，验证转换期间正确延迟，转换完成后正确处理
- **新增：测试视觉/逻辑状态分离**：验证 UI 绑定到 VisualState，内部逻辑使用 LogicalState，状态更新顺序正确
- **新增：测试组合转换记录**：验证子转换正确记录到历史，可以通过 GetCompositeTransitions 查询

### Integration Tests

- 测试完整的窗口生命周期（创建 -> 显示 -> 隐藏 -> 固定 -> 取消固定 -> 关闭）
- 测试在不同状态下切换的完整流程
- 测试状态转换与动画、AppBar、窗口样式的协调
- 测试多次快速状态切换的稳定性
- 测试状态转换失败时的错误处理和恢复（回滚机制）
- 测试窗口被系统强制关闭的场景（模拟系统注销、进程终止）
- 测试 `WindowHostController` 先于 `MainWindowViewModel` 释放的场景
- 测试 `WindowHostController` 在 Closed 事件后访问 `StateManager` 的防御性行为
- 测试组合副作用的完整流程（Pinned -> Hidden, Maximized -> Hidden）
- 测试超时保护在真实动画场景下的行为
- 测试反向同步机制（用户通过快捷键或任务栏操作窗口）
- **新增：测试命令模式完整流程**（CreatePlan -> Execute -> CommitTransition）
- **🔴 CRITICAL: 测试版本号并发控制在真实场景下的行为**
  - 快速连续点击按钮（A→B→C），验证过期任务被拒绝
  - 验证最终状态与最后一次点击一致
  - 验证 UI 和状态完全同步，无中间状态残留
- **🟠 CRITICAL: 测试 TryEnqueue 失败在真实场景下的行为**
  - 模拟 Dispatcher 队列满或关闭的场景
  - 验证 UI 不会出现部分更新
  - 验证状态机能正确恢复，后续操作正常
- **新增：测试 OS 同步事件排队在真实场景下的行为**（动画期间用户通过快捷键操作窗口）
- **新增：测试视觉/逻辑状态分离在 UI 绑定场景下的行为**（验证 UI 不会提前变化）
- **新增：测试组合转换记录在调试场景下的可用性**（查询子转换历史，验证完整性）

## Migration Checklist

在实施重构时，使用以下检查清单确保所有步骤都已完成：

**阶段 1：准备工作**
- [ ] 创建 `功能/主窗口/状态/窗口状态.cs` 文件
- [ ] 创建 `功能/主窗口/状态/窗口状态管理器.cs` 文件（包含所有架构改进）
- [ ] 🔴 **CRITICAL**: 实现 `TransitionPlan` 记录类型，包含 `TransitionId` 字段（防止竞态条件）
- [ ] 实现 `CompositeTransition` 记录类型（组合转换记录）
- [ ] 实现 `_transitionId` 版本号控制（并发控制）
- [ ] 实现 `VisualState` 和 `LogicalState` 属性（状态分离）
- [ ] 实现 `QueueSyncEvent` 和 `ProcessPendingSyncEvent` 方法（OS 同步事件排队）
- [ ] 🟠 **CRITICAL**: 在 `CreatePlan` 中实现 TryEnqueue 失败时的立即回滚逻辑
- [ ] 编译确保新文件无语法错误
- [ ] 为 `WindowStateManager` 编写单元测试（包括所有新机制）
- [ ] 🔴 **CRITICAL**: 编写 TransitionId 竞态条件测试（快速连续转换，验证过期 commit 被拒绝）
- [ ] 🟠 **CRITICAL**: 编写 TryEnqueue 失败测试（mock 返回 false，验证状态回滚）

**阶段 2：修改 WindowHostController**
- [ ] 在 `WindowHostController` 构造函数中创建 `WindowStateManager`
- [ ] 订阅 `StateChanged` 事件，实现 `OnWindowStateChanged` 方法
- [ ] 修改 `OnWindowStateChanged` 使用命令模式（CreatePlan -> Execute -> CommitTransition）
- [ ] 🔴 **CRITICAL**: 从 `plan.TransitionId` 获取版本号，而不是单独查询 `GetCurrentTransitionId()`
- [ ] 🟠 **CRITICAL**: 添加 `plan == null` 检查，确保 TryEnqueue 失败时不执行副作用
- [ ] 实现组合副作用方法（`ExecuteCompositeAsync`，记录子转换）
- [ ] 添加超时保护和异常处理（使用版本号验证）
- [ ] 实现补偿操作（Compensate 函数）
- [ ] 修改 `SyncFromOSWindowState` 使用 `QueueSyncEvent`（OS 同步事件排队）
- [ ] 在 `OnWindowClosed` 中释放 `StateManager`
- [ ] 编译确保无错误
- [ ] 🔴 **CRITICAL**: 手动测试快速连续点击，验证状态不会被过期 commit 覆盖
- [ ] 🟠 **CRITICAL**: 模拟 Dispatcher 失败，验证 UI 不会出现部分更新

**阶段 3：修改 MainWindowViewModel**
- [ ] 移除 `WindowStateManager` 字段（不再持有）
- [ ] 添加 `_currentState` 字段和 `CurrentState` 属性
- [ ] 实现 `SubscribeToStateManager` 和 `UnsubscribeFromStateManager` 方法
- [ ] 修改 `OnStateChanged` 绑定到 `VisualState`（视觉/逻辑状态分离）
- [ ] 移除 `Dispose` 方法（不再需要）
- [ ] 编译确保无错误

**阶段 4：排查引用点**
- [ ] 使用 IDE 查找所有 `IsWindowVisible` 引用
- [ ] 使用 IDE 查找所有 `IsDockPinned` 引用
- [ ] 使用 IDE 查找所有 `_viewModel.StateManager` 引用（应该不存在了）
- [ ] 使用 IDE 查找所有 `BeginTransition` 引用（需要改为 `CreatePlan`）
- [ ] 使用 IDE 查找所有 `CommitTransition()` 引用（需要添加 transitionId 参数）
- [ ] 记录所有引用位置（文件名、行号）

**阶段 5：迁移引用点**
- [ ] 迁移 `WindowHostController` 中的所有引用（使用 `_stateManager` 而非 `_viewModel.StateManager`）
- [ ] 迁移所有 `BeginTransition` 调用为 `CreatePlan`
- [ ] 迁移所有 `CommitTransition()` 调用为 `CommitTransition(transitionId)`
- [ ] 迁移所有 `RollbackTransition()` 调用为 `RollbackTransition(transitionId)`
- [ ] 迁移 `MainWindow.xaml.cs`（移除直接 Presenter 操作，添加 StateChanged 订阅）
- [ ] 迁移其他服务类中的引用
- [ ] 迁移 UI 层的绑定（如果有）
- [ ] 每迁移一个文件，立即编译并测试

**阶段 6：清理旧代码**
- [ ] 从 `MainWindowViewModel` 删除 `IsWindowVisible` 属性
- [ ] 从 `MainWindowViewModel` 删除 `IsDockPinned` 属性
- [ ] 删除 `PendingAction` 枚举和相关代码（不再需要）
- [ ] 编译确保无错误（如有错误，返回阶段 5）

**阶段 7：测试验证**
- [ ] 运行所有单元测试（包括新架构改进的测试）
- [ ] 测试命令模式执行计划（TransitionPlan 的创建和执行）
- [ ] 测试版本号并发控制（过期任务被正确拒绝）
- [ ] 🔴 **CRITICAL**: 测试 TransitionId 竞态条件防护
  - [ ] 快速连续调用 CreatePlan（A→B→C），验证每个 plan 有唯一 TransitionId
  - [ ] 模拟延迟提交，验证过期 transitionId 被 CommitTransition 拒绝
  - [ ] 验证最终状态与最后一次转换一致
- [ ] 🟠 **CRITICAL**: 测试 TryEnqueue 失败场景
  - [ ] Mock IDispatcher.TryEnqueue 返回 false
  - [ ] 验证 CreatePlan 返回 null
  - [ ] 验证 PendingState 被清除，_isTransitioning 被解锁
  - [ ] 验证 Controller 不执行任何副作用
- [ ] 测试视觉/逻辑状态分离（UI 不会提前变化）
- [ ] 测试 OS 同步事件排队（转换期间延迟同步）
- [ ] 测试组合转换记录（子转换正确记录到历史）
- [ ] 测试组合副作用（Pinned -> Hidden, Maximized -> Hidden）
- [ ] 测试超时保护和回滚机制
- [ ] 测试补偿操作（Compensate 函数正确执行）
- [ ] 测试错误处理（快速点击、非法转换等）
- [ ] 测试资源清理（窗口关闭、Dispose 调用）
- [ ] 手动测试所有窗口状态转换场景
- [ ] 🔴 **CRITICAL**: 手动测试快速连续点击场景（A→B→C），验证 UI 和状态完全同步
- [ ] 🟠 **CRITICAL**: 手动测试 Dispatcher 压力场景，验证状态机能正确恢复
- [ ] 性能测试（状态转换延迟、内存占用）

**阶段 8：文档更新**
- [ ] 更新代码注释
- [ ] 更新 API 文档
- [ ] 更新用户手册（如果有）
- [ ] 记录已知问题和限制

## Known Limitations and Future Work

**当前限制**:
1. **无状态持久化**: 窗口关闭后不保存状态，重启后总是从 `NotCreated` 开始
2. **无状态转换动画插值**: 状态转换是离散的，不支持平滑的动画插值
3. **组合副作用固定顺序**: `ExecuteCompositeAsync` 按顺序执行操作，不支持并行或自定义顺序

**未来改进方向**:
1. **状态持久化**: 将窗口状态保存到配置文件，重启后恢复
2. **状态转换策略注入**: 将状态转换矩阵提取为 `IStateTransitionPolicy` 接口，支持运行时替换
3. **状态转换动画**: 在状态转换时支持自定义动画曲线和插值
4. **状态历史回放**: 提供 UI 工具查看和回放状态转换历史，用于调试
5. **多窗口支持**: 如果未来需要支持多个主窗口，需要重新设计 `StateManager` 的生命周期管理
6. **状态漂移自动修正**: 定期检测 OS 窗口状态与 `CommittedState` 是否一致，自动修正漂移
7. **弱事件模式**: 使用 `WeakEventManager` 订阅 `StateChanged` 事件，防止内存泄漏（多窗口场景）

## Critical Design Decisions and Issue Resolutions

### 🔴 CRITICAL ISSUE #1: TransitionPlan missing transitionId (Race Condition)

**Problem Description**: 
When transitions overlap, race conditions can cause async state corruption:
- Scenario: A→B (transitionId=1) still executing when user triggers B→C (transitionId=2)
- Result: Old commit(1) arrives late and overwrites correct state
- Impact: Hard to reproduce bugs, intermittent failures, state corruption

**Root Cause**:
Original design had `TransitionPlan` without `TransitionId`:
```csharp
// ❌ PROBLEMATIC: No way to validate which transition is being committed
public record TransitionPlan(
    WindowState From,
    WindowState To,
    Func<Task> Execute,
    Func<Task>? Compensate = null
);

// Controller code
var plan = _stateManager.CreatePlan(newState);
int transitionId = GetCurrentTransitionId(); // Separate query - race condition!
await plan.Execute();
_stateManager.CommitTransition(transitionId); // May use wrong ID
```

**Solution**:
Include `TransitionId` directly in `TransitionPlan`:
```csharp
// ✅ FIXED: TransitionId is part of the plan
public record TransitionPlan(
    int TransitionId,        // 🟢 Added: Prevents race conditions
    WindowState From,
    WindowState To,
    Func<Task> Execute,
    Func<Task>? Compensate = null
);

// StateManager code
public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
{
    lock (_lock)
    {
        var transitionId = ++_transitionId; // Capture version
        PendingState = newState;
        _isTransitioning = true;
        
        return new TransitionPlan(
            TransitionId: transitionId,  // 🟢 Include in plan
            From: previousState,
            To: newState,
            Execute: async () => { /* ... */ },
            Compensate: async () => { /* ... */ }
        );
    }
}

// Controller code
var plan = _stateManager.CreatePlan(newState);
if (plan == null) return;

int transitionId = plan.TransitionId; // 🟢 Get from plan, not separate query
await plan.Execute();
_stateManager.CommitTransition(transitionId); // Guaranteed to match
```

**Validation Logic**:
```csharp
public void CommitTransition(int transitionId)
{
    lock (_lock)
    {
        // Version validation: reject stale transitions
        if (transitionId != _transitionId)
        {
            System.Diagnostics.Debug.WriteLine($"Commit rejected: stale transition (expected {_transitionId}, got {transitionId})");
            return; // 🟢 Stale commit is safely ignored
        }
        
        CommittedState = PendingState.Value;
        PendingState = null;
        _isTransitioning = false;
    }
}
```

**Benefits**:
- Eliminates race condition: TransitionId is atomically captured with the plan
- Stale commits are safely rejected: Version mismatch detection
- Simpler controller code: No need for separate `GetCurrentTransitionId()` method
- Thread-safe: All operations protected by lock

**Test Coverage**:
- Unit test: Rapid successive transitions, verify stale commits are rejected
- Integration test: User rapidly clicks buttons, verify state remains consistent
- Property-based test: Random transition sequences, verify no state corruption

---

### 🟠 CRITICAL ISSUE #2: _isTransitioning + dispatcher failure path (State/UI Inconsistency)

**Problem Description**:
When `TryEnqueue` fails, state rolls back but Controller may have already executed partial UI side effects:
- Scenario: `CreatePlan` sets `PendingState` and `_isTransitioning = true`, then `TryEnqueue` fails
- Result: State rolls back (`PendingState = null`), but Controller might have started executing UI changes
- Impact: UI and state become inconsistent, state machine may lock up

**Root Cause**:
Original design rolled back state after potential Controller execution:
```csharp
// ❌ PROBLEMATIC: Controller might execute before rollback
public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
{
    lock (_lock)
    {
        PendingState = newState;
        _isTransitioning = true;
        
        var plan = new TransitionPlan(/* ... */);
        
        // Event dispatch happens AFTER plan is returned
        bool enqueued = _dispatcher.TryEnqueue(() => { /* event */ });
        
        if (!enqueued)
        {
            // ❌ TOO LATE: Controller already has the plan and might be executing
            PendingState = null;
            _isTransitioning = false;
            return null;
        }
        
        return plan; // ❌ Controller receives plan even if dispatch might fail
    }
}
```

**Solution**:
Return `null` immediately when `TryEnqueue` fails, before Controller can execute:
```csharp
// ✅ FIXED: Rollback happens before Controller sees the plan
public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
{
    lock (_lock)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowStateManager));
        if (_isTransitioning) return null; // Prevent re-entry
        if (!CanTransitionTo(newState)) return null;
        
        var transitionId = ++_transitionId;
        PendingState = newState;
        _isTransitioning = true;
        
        var transition = new StateTransition(/* ... */);
        _transitionHistory.Enqueue(transition);
        
        // 🟢 Dispatch event BEFORE creating plan
        var handler = StateChanged;
        if (handler != null)
        {
            var eventArgs = new StateChangedEventArgs(/* ... */);
            
            bool enqueued = _dispatcher.TryEnqueue(() => 
            {
                lock (_lock)
                {
                    if (_disposed) return;
                }
                handler.Invoke(this, eventArgs);
            });
            
            if (!enqueued)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Failed to enqueue state change event");
                
                // 🟢 CRITICAL FIX: Rollback BEFORE returning null
                // Controller receives null and does NOT execute any side effects
                // This ensures state and UI consistency: either both succeed or both fail
                PendingState = null;
                _isTransitioning = false;
                return null; // 🟢 Controller gets null, won't execute
            }
        }
        
        // 🟢 Only create plan if dispatch succeeded
        return new TransitionPlan(
            TransitionId: transitionId,
            From: previousState,
            To: newState,
            Execute: async () => { /* ... */ },
            Compensate: async () => { /* ... */ }
        );
    }
}
```

**Controller Handling**:
```csharp
private async void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
{
    var plan = _stateManager.CreatePlan(args.CurrentState, args.Reason);
    
    // 🟢 If plan is null, do NOT execute any side effects
    if (plan == null)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to create transition plan");
        return; // 🟢 Early exit, no UI changes
    }
    
    // Only execute if plan was successfully created
    int transitionId = plan.TransitionId;
    
    try
    {
        await plan.Execute(); // Execute UI side effects
        _stateManager.CommitTransition(transitionId);
    }
    catch (Exception ex)
    {
        _stateManager.RollbackTransition(transitionId, $"Exception: {ex.Message}");
        if (plan.Compensate != null)
        {
            await plan.Compensate();
        }
    }
}
```

**Benefits**:
- Prevents state/UI inconsistency: Controller only executes if dispatch succeeds
- Fail-fast behavior: Errors detected immediately, not after partial execution
- Simpler error handling: No need to compensate for partial UI changes
- State machine safety: `_isTransitioning` is unlocked immediately on failure

**Additional Safety Measures**:
1. **Timeout Protection**: Controller adds timeout to detect stuck animations
   ```csharp
   var completedTask = await Task.WhenAny(animationTask, Task.Delay(_animationTimeoutMs));
   if (completedTask != animationTask)
   {
       _stateManager.RollbackTransition(transitionId, "Animation timeout");
       return;
   }
   ```

2. **Exception Handling**: All side effects wrapped in try-catch
   ```csharp
   try
   {
       await plan.Execute();
       _stateManager.CommitTransition(transitionId);
   }
   catch (Exception ex)
   {
       _stateManager.RollbackTransition(transitionId, $"Exception: {ex.Message}");
       if (plan.Compensate != null)
       {
           await plan.Compensate();
       }
   }
   ```

**Test Coverage**:
- Unit test: Mock `IDispatcher.TryEnqueue` to return false, verify state rollback
- Unit test: Verify Controller receives null and does not execute side effects
- Integration test: Simulate dispatcher failure, verify UI remains unchanged
- Property-based test: Random dispatcher failures, verify state consistency

---

### 架构改进 1：命令模式状态机（Command-Pattern State Machine）

**问题描述**: 
原设计中 `StateManager` 定义状态但 `WindowHostController` 执行副作用，状态机不控制实际状态变化。`BeginTransition` 直接触发事件并设置 `PendingState`，但副作用的执行完全由 Controller 控制，StateManager 无法感知副作用的执行细节。

**解决方案（命令模式）**:
- `StateManager` 返回 `TransitionPlan`（执行计划）而不是直接触发事件
- `TransitionPlan` 包含 `Execute` 和 `Compensate` 函数
- Controller 执行计划，成功后调用 `CommitTransition(transitionId)` 提交，失败则调用 `RollbackTransition(transitionId)` 回滚
- 使用版本号 `transitionId` 验证，防止过期任务执行

**实现细节**:
```csharp
public record TransitionPlan(
    WindowState From,
    WindowState To,
    Func<Task> Execute,
    Func<Task>? Compensate = null
);

// StateManager
public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
{
    // 验证转换合法性，设置 PendingState
    var transitionId = ++_transitionId;
    PendingState = newState;
    _isTransitioning = true;
    
    return new TransitionPlan(
        From: CommittedState,
        To: newState,
        Execute: async () => { /* 由 Controller 实现 */ },
        Compensate: async () => { /* 补偿操作 */ }
    );
}

// Controller
var plan = _stateManager.CreatePlan(newState);
if (plan != null)
{
    try
    {
        await plan.Execute();
        _stateManager.CommitTransition(transitionId);
    }
    catch
    {
        _stateManager.RollbackTransition(transitionId);
        if (plan.Compensate != null)
        {
            await plan.Compensate();
        }
    }
}
```

**优势**:
- 职责清晰：StateManager 负责状态验证和转换计划，Controller 负责副作用执行
- 可测试性强：可以 mock TransitionPlan，独立测试 StateManager 和 Controller
- 支持补偿操作：失败时可以执行 Compensate 恢复原状态
- 版本号控制：防止过期任务执行，解决并发问题

### 架构改进 2：视觉状态与逻辑状态分离（Visual/Logical State Separation）

**问题描述**: 
原设计中 UI 绑定到 `CurrentState`（等同于 `PendingState ?? CommittedState`），导致 UI 在转换完成前就看到 `PendingState`，图标提前变化但窗口还没隐藏，用户体验不一致。

**解决方案（状态分离）**:
- 分离 `VisualState`（已提交）和 `LogicalState`（待定）
- UI 绑定到 `VisualState`（等同于 `CommittedState`），只有副作用完成后才更新
- 内部逻辑使用 `LogicalState`（等同于 `PendingState ?? CommittedState`）

**实现细节**:
```csharp
// StateManager
public WindowState CommittedState { get; private set; }
public WindowState? PendingState { get; private set; }

// 视觉状态：UI 绑定到此状态
public WindowState VisualState => CommittedState;

// 逻辑状态：内部逻辑使用
public WindowState LogicalState => PendingState ?? CommittedState;

// 向后兼容：CurrentState 指向 LogicalState
public WindowState CurrentState => LogicalState;

// ViewModel
public void SubscribeToStateManager(WindowStateManager stateManager)
{
    stateManager.StateChanged += OnStateChanged;
    
    // UI 绑定到 VisualState
    CurrentState = stateManager.VisualState;
}

private void OnStateChanged(object? sender, StateChangedEventArgs args)
{
    // 只有 CommitTransition 后才更新 UI
    CurrentState = _stateManager.VisualState;
}
```

**优势**:
- UI 一致性：图标和窗口状态同步变化，避免提前变化
- 逻辑正确性：内部逻辑可以使用 PendingState 做决策（如防重入）
- 易于理解：VisualState 和 LogicalState 语义清晰

### 架构改进 3：基于版本号的并发控制（Version-Based Concurrency Control）

**问题描述**: 
原设计中简单的 `_isTransitioning` 布尔标志在异步 + Dispatcher 场景有竞态条件。如果动画超时或异常，可能出现过期的 `CommitTransition` 调用，导致状态不一致。

**解决方案（版本号控制）**:
- 使用递增的 `_transitionId` 跟踪转换
- 转换开始时捕获版本号，完成时验证版本号匹配
- 版本号不匹配则拒绝转换完成

**实现细节**:
```csharp
// StateManager
private int _transitionId = 0;

public TransitionPlan? CreatePlan(WindowState newState, string? reason = null)
{
    var transitionId = ++_transitionId; // 捕获版本号
    PendingState = newState;
    _isTransitioning = true;
    
    return new TransitionPlan(/* ... */);
}

public void CommitTransition(int transitionId)
{
    lock (_lock)
    {
        // 版本号验证：防止过期任务执行
        if (transitionId != _transitionId)
        {
            System.Diagnostics.Debug.WriteLine($"Commit rejected: stale transition");
            return;
        }
        
        CommittedState = PendingState.Value;
        PendingState = null;
        _isTransitioning = false;
    }
}

// Controller
var plan = _stateManager.CreatePlan(newState);
var transitionId = _stateManager.CurrentTransitionId; // 捕获版本号

await Task.Delay(1000); // 模拟动画

_stateManager.CommitTransition(transitionId); // 验证版本号
```

**优势**:
- 完全消除竞态条件：过期任务被正确拒绝
- 支持并发场景：多个异步任务可以安全执行
- 易于调试：可以通过版本号追踪转换历史

**替代方案（未采用）**:
- **方案 B**: 使用 `CancellationToken` 取消过期任务
  - 缺点：需要在所有异步操作中传递 token，代码复杂度高

### 架构改进 4：组合转换记录（Composite Transition Recording）

**问题描述**: 
原设计中 `Pinned -> Hidden` 记录为单个转换，但实际执行 `Pinned -> Windowed -> Hidden`，丢失中间状态，难以调试和追踪。

**解决方案（显式拆分 + 记录子转换）**:
- 保留组合转换（`Pinned -> Hidden`），但记录 `TransitionType = Composite`
- 记录 `SubTransitions = [Pinned -> Windowed, Windowed -> Hidden]`
- 允许在子转换边界处中断（如果需要）

**实现细节**:
```csharp
// StateManager
public record CompositeTransition(
    WindowState From,
    WindowState To,
    List<StateTransition> SubTransitions,
    DateTime Timestamp
);

private readonly List<CompositeTransition> _compositeTransitions = new();

public void RecordSubTransition(int transitionId, StateTransition subTransition)
{
    lock (_lock)
    {
        if (transitionId != _transitionId)
        {
            return; // 过期任务，忽略
        }
        
        // 记录子转换到当前组合转换
        // ...
    }
}

// Controller
private async Task ExecuteCompositeAsync(int transitionId, params StateTransition[] subTransitions)
{
    foreach (var subTransition in subTransitions)
    {
        // 记录子转换
        _stateManager.RecordSubTransition(transitionId, subTransition);
        
        // 执行子转换的副作用
        await ExecuteSubTransition(subTransition);
    }
}
```

**优势**:
- 完整的历史记录：可以查询子转换，了解完整的执行路径
- 易于调试：可以定位到具体哪个子转换失败
- 支持中断：可以在子转换边界处中断（如果需要）

**替代方案（未采用）**:
- **方案 A**: 显式拆分所有组合转换（`Pinned -> Windowed`, `Windowed -> Hidden`）
  - 缺点：状态图复杂，用户需要点击两次才能完成隐藏

### 架构改进 5：OS 同步事件排队（OS Synchronization Event Queuing）

**问题描述**: 
原设计中 `SyncFromOSWindowState` 在转换期间强行插入新状态，打断当前动画。例如，窗口正在执行 `Windowed -> Hidden` 动画，用户通过 Win+Up 快捷键最大化窗口，导致动画中断。

**解决方案（事件排队）**:
- 转换期间延迟同步或忽略外部事件
- 使用外部事件队列 `_pendingSyncEvent`，转换完成后处理
- 只处理最新的同步事件（覆盖旧事件）

**实现细节**:
```csharp
// StateManager
private WindowState? _pendingSyncEvent = null;

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

public void CommitTransition(int transitionId)
{
    lock (_lock)
    {
        // ... 提交状态 ...
        
        _isTransitioning = false;
        
        // 处理延迟的 OS 同步事件
        ProcessPendingSyncEvent();
    }
}

private void ProcessPendingSyncEvent()
{
    if (_pendingSyncEvent.HasValue)
    {
        var osState = _pendingSyncEvent.Value;
        _pendingSyncEvent = null;
        SyncToOSState(osState);
    }
}

// Controller
public void SyncFromOSWindowState()
{
    var osState = GetOSWindowState();
    
    // 使用事件排队机制
    _stateManager.QueueSyncEvent(osState);
}
```

**优势**:
- 动画流畅：转换期间不会被外部事件打断
- 状态一致：转换完成后处理最新的 OS 状态，避免中间状态
- 简单可靠：只保留最新事件，避免队列积压

**替代方案（未采用）**:
- **方案 B**: 转换期间完全忽略外部事件
  - 缺点：可能丢失重要的 OS 状态变化

### 问题 1：状态"提前提交" + 副作用失败 = 状态漂移

**问题描述**: 
原设计中 `TransitionTo` 在副作用（动画/样式）完成前就改变 `CurrentState` 并返回 `true`，导致状态"已提交"但窗口尚未反映该状态。如果 `presenter.Maximize()` 失败或被系统拒绝，会出现 `CurrentState = Maximized` 但实际窗口还是 `Windowed` 的状态漂移。

**解决方案（方案 A - PendingState）**:
- 引入 `PendingState` 和 `CommittedState` 两个状态属性
- `BeginTransition` 设置 `PendingState`，但不修改 `CommittedState`
- 副作用成功后调用 `CommitTransition()` 提交状态
- 副作用失败时调用 `RollbackTransition()` 回滚状态
- `CurrentState` 属性返回 `PendingState ?? CommittedState`

**实现细节**:
```csharp
public WindowState CommittedState { get; private set; }
public WindowState? PendingState { get; private set; }
public WindowState CurrentState => PendingState ?? CommittedState;

public bool BeginTransition(WindowState newState, string? reason = null)
{
    // 设置 PendingState，触发 StateChanged 事件
    PendingState = newState;
    _isTransitioning = true;
    // 触发事件...
    return true;
}

public void CommitTransition()
{
    if (PendingState.HasValue)
    {
        CommittedState = PendingState.Value;
        PendingState = null;
    }
    _isTransitioning = false;
}

public void RollbackTransition(string? reason = null)
{
    if (PendingState.HasValue)
    {
        System.Diagnostics.Debug.WriteLine($"Transition rolled back: {reason}");
        PendingState = null;
    }
    _isTransitioning = false;
}
```

**优势**:
- 完全消除状态漂移风险：副作用失败时自动回滚
- 状态语义清晰：`CommittedState` 表示稳定状态，`PendingState` 表示转换中状态
- 易于调试：可以观察 `PendingState` 和 `CommittedState` 的差异
- 支持状态漂移检测：可以定期检查 OS 窗口状态与 `CommittedState` 是否一致

**替代方案（未采用）**:
- **方案 B**: 保留当前设计，添加 `ActualState`（真实状态），用于检测漂移并自动修正
  - 缺点：增加状态复杂度，需要定期轮询 OS 状态，性能开销大

### 问题 2：_isTransitioning 可能"锁死"

**问题描述**: 
原设计中 `TryEnqueue` 失败时 `_isTransitioning = true` 但事件没触发，依赖调用方 finally 保证。现实项目中可能有人忘写 finally 或异常路径没走到，导致状态机永久锁死。

**解决方案（内部兜底解锁）**:
- 在 `BeginTransition` 内部，如果 `TryEnqueue` 失败，立即回滚并解锁
- 不依赖调用方的 finally 块，状态机自己保证一致性

**实现细节**:
```csharp
public bool BeginTransition(WindowState newState, string? reason = null)
{
    lock (_lock)
    {
        // ... 验证逻辑 ...
        
        PendingState = newState;
        _isTransitioning = true;
        
        // 触发事件
        bool enqueued = _dispatcher.TryEnqueue(() => { /* 事件处理 */ });
        
        if (!enqueued)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: Failed to enqueue state change event");
            
            // 兜底机制：调度失败时立即回滚并解锁
            PendingState = null;
            _isTransitioning = false;
            return false;
        }
        
        return true;
    }
}
```

**优势**:
- 完全消除锁死风险：调度失败时自动解锁
- 不依赖调用方：状态机内部保证一致性
- 简化调用方代码：不需要 finally 块

**额外保护（超时机制）**:
在 `WindowHostController` 中添加超时保护：
```csharp
private async void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
{
    try
    {
        Task animationTask = /* 执行副作用 */;
        var completedTask = await Task.WhenAny(animationTask, Task.Delay(_animationTimeoutMs));
        
        if (completedTask != animationTask)
        {
            // 超时视为失败，回滚状态
            _stateManager.RollbackTransition("Animation timeout");
            return;
        }
        
        // 成功，提交状态
        _stateManager.CommitTransition();
    }
    catch (Exception ex)
    {
        // 异常视为失败，回滚状态
        _stateManager.RollbackTransition($"Animation exception: {ex.Message}");
    }
}
```

### 问题 3：Windowed 作为"唯一中转站"过重

**问题描述**: 
原设计中所有路径都必须经过 Windowed（Pinned -> Windowed -> Hidden, Maximized -> Windowed -> Hidden），状态链变长，PendingAction 越来越多，会慢慢变成"隐式状态机"。

**解决方案（允许直接转换）**:
- 允许 `Pinned -> Hidden` 和 `Maximized -> Hidden` 直接转换
- 在内部自动执行组合副作用（Unpin + Hide, Restore + Hide）
- 状态图简单（只有 7 条边），用户体验流畅（一次点击完成隐藏）

**新的状态转换矩阵**:
```csharp
private static readonly Dictionary<WindowState, HashSet<WindowState>> _defaultAllowedTransitions = new()
{
    [WindowState.NotCreated] = new() { WindowState.Windowed },
    [WindowState.Hidden] = new() { WindowState.Windowed },
    [WindowState.Windowed] = new() { WindowState.Hidden, WindowState.Maximized, WindowState.Pinned },
    [WindowState.Maximized] = new() { WindowState.Windowed, WindowState.Hidden }, // 新增直接转换
    [WindowState.Pinned] = new() { WindowState.Windowed, WindowState.Hidden }     // 新增直接转换
};
```

**组合副作用实现**:
```csharp
private async void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
{
    Task animationTask = (args.PreviousState, args.CurrentState) switch
    {
        // 简单转换
        (WindowState.Windowed, WindowState.Hidden) => ExecuteHideAnimationAsync(),
        (WindowState.Hidden, WindowState.Windowed) => ExecuteShowAnimationAsync(),
        
        // 组合副作用：Maximized -> Hidden（先还原再隐藏）
        (WindowState.Maximized, WindowState.Hidden) => ExecuteCompositeAsync(
            RestoreFromMaximizedModeAsync,
            ExecuteHideAnimationAsync
        ),
        
        // 组合副作用：Pinned -> Hidden（先取消固定再隐藏）
        (WindowState.Pinned, WindowState.Hidden) => ExecuteCompositeAsync(
            RestoreFromPinnedModeAsync,
            ExecuteHideAnimationAsync
        ),
        
        _ => Task.CompletedTask
    };
    
    // 执行副作用...
}

private async Task ExecuteCompositeAsync(params Func<Task>[] operations)
{
    foreach (var operation in operations)
    {
        await operation();
    }
}
```

**优势**:
- 状态图简单：只有 7 条边，易于理解和维护
- 用户体验流畅：一次点击完成复杂操作
- 逻辑复杂度在内部：调用方代码简化，不需要 PendingAction

**移除 PendingAction**:
由于支持直接转换，不再需要 `PendingAction` 机制：
```csharp
// 旧代码（使用 PendingAction）
if (currentState == WindowState.Pinned)
{
    _stateManager.BeginTransition(WindowState.Windowed, "Unpin before hide");
    _pendingAction = PendingAction.HideAfterRestore; // 设置延迟意图
    return;
}

// 新代码（直接转换）
if (currentState == WindowState.Pinned)
{
    _stateManager.BeginTransition(WindowState.Hidden, "User requested hide");
}
```

### 问题 4：TransitionHistory 没线程隔离风险

**问题描述**: 
原设计中 `_transitionHistory.Enqueue()` 虽然有 lock，但如果暴露 `GetHistory()` 外部遍历时会炸（集合修改异常）。

**解决方案（返回副本）**:
```csharp
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
```

**优势**:
- 完全线程安全：外部遍历时不会受内部修改影响
- 简单可靠：不需要复杂的并发集合

**性能考虑**:
- 如果历史记录很大（>1000 条），复制开销可能较大
- 当前设计限制历史记录为 100 条，复制开销可接受
- 如果未来需要支持更大的历史记录，可以考虑使用 `ImmutableList` 或分页查询

### 问题 5：ViewModel 持有 StateManager（耦合略高）

**问题描述**: 
原设计中 `MainWindowViewModel` 持有 `WindowStateManager`，`WindowHostController` 通过 `_viewModel.StateManager` 访问。问题是 `StateManager` 是 Domain 层，应该由 Controller 持有，ViewModel 只订阅。

**解决方案（调整架构）**:
```
WindowHostController
  └── StateManager（核心，持有所有权）
MainWindowViewModel
  └── 只订阅 StateChanged 事件
```

**实现细节**:
```csharp
// WindowHostController
public WindowHostController(Window window, MainWindowViewModel viewModel)
{
    _window = window;
    _viewModel = viewModel;
    
    // 创建并持有 StateManager
    _stateManager = WindowStateManager.CreateForUIThread();
    _stateManager.StateChanged += OnWindowStateChanged;
    
    // ViewModel 订阅状态变化
    _viewModel.SubscribeToStateManager(_stateManager);
    
    _window.Closed += OnWindowClosed;
}

private void OnWindowClosed(object sender, WindowEventArgs args)
{
    // 清理事件订阅
    _stateManager.StateChanged -= OnWindowStateChanged;
    _viewModel.UnsubscribeFromStateManager(_stateManager);
    
    // 释放 StateManager（拥有所有权）
    _stateManager.Dispose();
}

// MainWindowViewModel
private WindowState _currentState = WindowState.NotCreated;

public WindowState CurrentState
{
    get => _currentState;
    private set
    {
        if (_currentState != value)
        {
            _currentState = value;
            RaisePropertyChanged(nameof(CurrentState));
        }
    }
}

public void SubscribeToStateManager(WindowStateManager stateManager)
{
    stateManager.StateChanged += OnStateChanged;
    CurrentState = stateManager.CurrentState; // 同步初始状态
}

public void UnsubscribeFromStateManager(WindowStateManager stateManager)
{
    if (stateManager != null)
    {
        stateManager.StateChanged -= OnStateChanged;
    }
}

private void OnStateChanged(object? sender, StateChangedEventArgs args)
{
    CurrentState = args.CurrentState;
}
```

**优势**:
- 职责清晰：Controller 持有 StateManager，负责协调状态转换和副作用执行
- 降低耦合：ViewModel 只订阅事件，不持有 StateManager
- 生命周期简单：StateManager 的生命周期与 Controller 一致
- 易于测试：可以 mock StateManager 注入到 Controller

**生命周期保证**:
- `WindowHostController` 的生命周期 >= `MainWindowViewModel` 的生命周期
- `WindowHostController.OnWindowClosed` 中先取消 ViewModel 订阅，再释放 StateManager
- `MainWindowViewModel` 不需要 Dispose 方法（不持有资源）

## Summary of Resolutions

| Issue | Resolution | Impact |
|-------|-----------|--------|
| 🔴 **CRITICAL #1: TransitionPlan missing transitionId** | Include TransitionId in TransitionPlan record, validate on commit/rollback | Eliminates race conditions, prevents async state corruption |
| 🟠 **CRITICAL #2: _isTransitioning + dispatcher failure** | Return null from CreatePlan when TryEnqueue fails, rollback before Controller execution | Prevents state/UI inconsistency, fail-fast behavior |
| 1. Command-Pattern State Machine | StateManager 返回 TransitionPlan，Controller 执行计划并提交/回滚 | 职责清晰，可测试性强，支持补偿操作 |
| 2. Visual/Logical State Separation | 分离 VisualState（UI 绑定）和 LogicalState（内部逻辑） | UI 一致性，避免图标提前变化 |
| 3. Version-Based Concurrency Control | 使用递增的 transitionId 验证，拒绝过期任务 | 完全消除竞态条件，支持并发场景 |
| 4. Composite Transition Recording | 记录子转换到 CompositeTransition，保留完整历史 | 易于调试，可以追踪完整执行路径 |
| 5. OS Synchronization Event Queuing | 转换期间延迟外部同步事件，转换完成后处理 | 动画流畅，状态一致 |
| 6. State Commit Semantics | 引入 PendingState 机制，副作用成功后才提交状态，失败则回滚 | 完全消除状态漂移风险 |
| 7. Scheduling Failure Handling | 在 CreatePlan 内部兜底解锁，添加超时保护 | 完全消除锁死风险 |
| 8. Windowed as Central Hub | 允许 Pinned/Maximized -> Hidden 直接转换，内部执行组合副作用 | 状态图简化，用户体验流畅 |
| 9. TransitionHistory Thread Safety | GetTransitionHistory 和 GetCompositeTransitions 返回副本 | 完全线程安全 |
| 10. ViewModel Ownership | StateManager 移到 WindowHostController，ViewModel 只订阅 | 降低耦合，职责清晰 |

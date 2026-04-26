using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Appearance;
using Docked_AI.Features.MainWindow.Placement;
using Docked_AI.Features.MainWindow.Entry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;

namespace Docked_AI.Features.MainWindow.Visibility
{
    /// <summary>
    /// 窗口宿主控制器 - 状态转换执行器，协调所有窗口操作
    /// 
    /// 【文件职责】
    /// 1. 作为状态转换的执行器，将 StateManager 的计划转换为实际的 UI 操作
    /// 2. 协调所有窗口相关服务（布局、外观、动画、AppBar）
    /// 3. 管理窗口生命周期（初始化、显示、隐藏、关闭）
    /// 4. 处理 OS 窗口状态同步（最大化、还原、最小化）
    /// 5. 实现复杂的状态转换流程（组合转换、两步转换）
    /// 
    /// 【架构设计】
    /// 
    /// 三层架构：
    /// - WindowStateManager: 状态逻辑层（状态机、转换验证、历史记录）
    /// - WindowHostController: 执行层（动画、样式、布局的实际操作）
    /// - MainWindow: 表示层（UI 容器、事件订阅、用户交互）
    /// 
    /// 为什么需要 Controller？
    /// 1. 分离关注点：StateManager 不关心 UI 如何实现，Controller 不关心状态逻辑
    /// 2. 可测试性：可以独立测试 StateManager 和 Controller
    /// 3. 可扩展性：可以替换不同的 Controller 实现（如测试用的 Mock Controller）
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 初始化流程：
    ///   1. 构造函数创建所有服务（布局、外观、动画、StateManager）
    ///   2. 订阅 StateManager.StateChanged 事件
    ///   3. ViewModel 订阅 StateManager（通过 Controller 调用）
    ///   4. InitializeWindow() 配置窗口初始状态（位置、样式、事件）
    ///   5. 窗口移到屏幕外，等待 RequestSlideIn() 触发首次显示
    /// 
    /// 首次显示流程（RequestSlideIn）：
    ///   1. 刷新布局信息（屏幕尺寸、工作区）
    ///   2. 设置窗口到目标位置（不是屏幕外）
    ///   3. 标记首次显示完成，记录时间（用于保护期）
    ///   4. 更新状态到 Windowed
    ///   5. 调用 Activate() 显示窗口（利用系统内置动画）
    /// 
    /// 标准状态转换流程：
    ///   1. 用户触发操作（如点击固定按钮）
    ///   2. Controller 调用 TryRequestTransition(targetState)
    ///   3. StateManager.CreatePlan() 创建转换计划
    ///   4. StateManager 触发 StateChanged 事件
    ///   5. Controller.OnWindowStateChanged() 执行副作用
    ///   6. 副作用成功 → StateManager.CommitTransition()
    ///   7. 副作用失败 → StateManager.RollbackTransition()
    /// 
    /// 组合转换流程（如 Pinned → Hidden）：
    ///   1. 用户触发隐藏操作
    ///   2. StateManager 允许直接转换 Pinned → Hidden
    ///   3. Controller.OnWindowStateChanged() 检测到组合转换
    ///   4. ExecuteCompositeAsync() 按顺序执行子转换：
    ///      - 子转换1: Pinned → Windowed（取消固定）
    ///      - 子转换2: Windowed → Hidden（隐藏窗口）
    ///   5. 记录子转换到历史
    ///   6. 提交最终状态
    /// 
    /// 两步转换流程（如 Pinned → Maximized）：
    ///   1. 用户触发最大化操作
    ///   2. StateManager 不允许直接转换 Pinned → Maximized
    ///   3. TransitionThroughWindowedAsync() 执行两步转换：
    ///      - 步骤1: Pinned → Windowed
    ///      - 等待步骤1完成（轮询 CommittedState）
    ///      - 步骤2: Windowed → Maximized
    ///   4. 每步转换都是独立的状态转换，有独立的 transitionId
    /// 
    /// OS 状态同步流程：
    ///   1. 用户通过 Win+↑ 最大化窗口
    ///   2. AppWindow.Changed 事件触发
    ///   3. MainWindow.OnAppWindowChanged() 调用 SyncFromOSWindowState()
    ///   4. StateManager.QueueSyncEvent() 排队同步事件
    ///   5. 如果正在转换，事件延迟；否则立即同步
    /// 
    /// 【关键依赖关系】
    /// - Window: WinUI 窗口对象，提供 AppWindow、Activate() 等 API
    /// - MainWindowViewModel: 状态容器，订阅 StateManager 事件
    /// - WindowLayoutService: 布局服务，计算窗口位置和尺寸
    /// - TitleBarService: 标题栏服务，配置标准/固定模式的标题栏
    /// - BackdropService: 背景服务，切换 Acrylic/Mica 背景
    /// - SlideAnimationController: 动画控制器，执行滑动动画
    /// - WindowStateManager: 状态管理器，提供状态转换逻辑
    /// 
    /// 【潜在副作用】
    /// 1. 窗口位置和尺寸变化（MoveAndResize、SetWindowPos）
    /// 2. 窗口样式变化（SetWindowLongPtr、DwmSetWindowAttribute）
    /// 3. AppBar 注册/注销（SHAppBarMessage）
    /// 4. 窗口激活和焦点变化（Activate、SetForegroundWindow）
    /// 5. 背景和标题栏样式变化（BackdropService、TitleBarService）
    /// 6. 动画执行（SlideAnimationController）
    /// 7. 窗口子类化（SetWindowProc）
    /// 
    /// 【重构风险点】
    /// 1. RequestSlideIn() 的调用时机：
    ///    - 必须在窗口创建完成后、Activate() 之前调用
    ///    - 过早调用会导致窗口句柄未创建
    ///    - 过晚调用会导致窗口已显示，动画失效
    /// 2. OnWindowStateChanged() 的副作用执行：
    ///    - 必须根据 (PreviousState, CurrentState) 匹配正确的副作用
    ///    - 如果匹配错误，会执行错误的动画或样式
    ///    - 组合转换必须按顺序执行子转换
    /// 3. 首次显示保护期（InitialShowProtectionPeriod）：
    ///    - 防止动画完成后立即因失去焦点而隐藏
    ///    - 如果保护期太短，窗口会闪现后立即隐藏
    ///    - 如果保护期太长，用户点击其他窗口时不会自动隐藏
    /// 4. AppBar 注册/注销：
    ///    - 必须成对调用 RegisterAppBarIfNeeded() 和 RemoveAppBar()
    ///    - 如果忘记注销，AppBar 会一直占用屏幕空间
    /// 5. 窗口样式切换：
    ///    - ApplyPinnedWindowStyle() 和 RestoreStandardWindowStyle() 必须成对
    ///    - 如果忘记还原，窗口样式会保持固定模式
    /// 6. 窗口子类化：
    ///    - TrySubclassWindow() 只能调用一次
    ///    - 如果重复调用，会覆盖原始的 WindowProc
    /// 7. 事件订阅：
    ///    - OnWindowClosed() 必须取消所有事件订阅
    ///    - 否则导致内存泄漏
    /// 8. 动画超时：
    ///    - 如果动画超时，必须回滚状态
    ///    - 否则 StateManager 认为转换成功，但 UI 未更新
    /// </summary>
    internal sealed class WindowHostController
    {
        // 核心依赖
        private readonly Window _window;
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowLayoutService _layoutService;
        private readonly WindowLayoutState _state;
        private readonly TitleBarService _titleBarService;
        private readonly BackdropService _backdropService;
        private readonly SlideAnimationController _animationController;
        private readonly WindowStateManager _stateManager;
        private readonly int _animationTimeoutMs;

        // 状态标志
        private bool _animationStarted;  // 是否已执行首次显示动画
        private bool _isAppBarRegistered;  // 是否已注册 AppBar
        private bool _isApplyingPinnedBounds;  // 是否正在应用固定模式边界（防重入）
        private bool _hasCapturedBaseWindowStyle;  // 是否已捕获基础窗口样式
        private bool _hasCapturedBaseExtendedWindowStyle;  // 是否已捕获扩展窗口样式
        private bool _isWindowSubclassed;  // 是否已子类化窗口
        private bool _isInitialShowComplete;  // 标记首次显示是否完成
        private DateTime _initialShowCompletedTime;  // 首次显示完成的时间
        
        // Win32 相关
        private IntPtr _hwnd;  // 窗口句柄
        private readonly uint _appBarMessageId;  // AppBar 消息 ID
        private IntPtr _baseWindowStyle;  // 基础窗口样式（用于还原）
        private IntPtr _baseExtendedWindowStyle;  // 扩展窗口样式（用于还原）
        private IntPtr _originalWindowProc;  // 原始窗口过程（用于子类化）
        private readonly VisibilityWin32Api.WindowProc _windowProcDelegate;  // 窗口过程委托

        // 常量配置
        private const int DefaultAnimationTimeoutMs = 2000;
        private static readonly TimeSpan TransitionThroughWindowedTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan TransitionPollInterval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan IntermediateTransitionDelay = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan SlideAnimationDelay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan PinnedModeDelay = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan MaximizedModeDelay = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan InitialShowProtectionPeriod = TimeSpan.FromMilliseconds(500);  // 首次显示后的保护期

        /// <summary>
        /// 构造函数 - 初始化所有服务和状态管理器
        /// 
        /// 【初始化顺序】
        /// 1. 保存窗口和 ViewModel 引用
        /// 2. 创建布局服务和状态
        /// 3. 创建外观服务（标题栏、背景）
        /// 4. 创建动画控制器
        /// 5. 注册 AppBar 消息 ID
        /// 6. 创建窗口过程委托（用于子类化）
        /// 7. 创建 StateManager 并订阅事件
        /// 8. ViewModel 订阅 StateManager
        /// 9. 初始化窗口
        /// 
        /// 【设计原因】
        /// 为什么在构造函数中创建 StateManager？
        /// - StateManager 的生命周期与 Controller 相同
        /// - Controller 负责管理 StateManager 的创建和销毁
        /// - ViewModel 只订阅 StateManager，不持有引用
        /// </summary>
        public WindowHostController(Window window, MainWindowViewModel viewModel, int animationTimeoutMs = DefaultAnimationTimeoutMs)
        {
            _window = window;
            _viewModel = viewModel;
            _layoutService = new WindowLayoutService();
            _state = _layoutService.CreateInitialState();
            _titleBarService = new TitleBarService();
            _backdropService = new BackdropService();
            _animationController = new SlideAnimationController(_window, _state);
            _appBarMessageId = VisibilityWin32Api.RegisterWindowMessage("DockedAI_AppBarMessage");
            _windowProcDelegate = WindowProc;
            _animationTimeoutMs = animationTimeoutMs;

            // Create and hold StateManager
            _stateManager = WindowStateManager.CreateForUIThread();
            _stateManager.StateChanged += OnWindowStateChanged;

            // ViewModel subscribes to state changes
            _viewModel.SubscribeToStateManager(_stateManager);

            InitializeWindow();
        }

        /// <summary>
        /// 标记初始化完成，解除事件屏蔽
        /// 由托盘管理器在窗口创建完成后调用
        /// </summary>
        public void SetInitializingComplete()
        {
            System.Diagnostics.Debug.WriteLine("WindowHostController: Initialization complete");
        }

        /// <summary>
        /// 请求执行首次显示（由托盘图标点击触发）
        /// 利用 Activate() 的内置动画，这是首次创建窗口时唯一不会闪现的方案
        /// </summary>
        public void RequestSlideIn()
        {
            System.Diagnostics.Debug.WriteLine($"[WindowHostController] RequestSlideIn called, _animationStarted={_animationStarted}");
            
            if (_animationStarted)
            {
                System.Diagnostics.Debug.WriteLine("[WindowHostController] RequestSlideIn: Already shown, ignoring");
                return;
            }

            _animationStarted = true;
            System.Diagnostics.Debug.WriteLine("[WindowHostController] RequestSlideIn: Showing window with built-in animation");

            try
            {
                // 1. 刷新布局信息
                _layoutService.Refresh(_state);
                System.Diagnostics.Debug.WriteLine($"[WindowHostController] Layout refreshed: TargetX={_state.TargetX}, TargetY={_state.TargetY}, W={_state.WindowWidth}, H={_state.WindowHeight}");
                
                // 2. 设置窗口到目标位置（不是屏幕外）
                _state.CurrentX = _state.TargetX;
                _state.CurrentY = _state.TargetY;
                
                _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                    _state.TargetX,  // 直接设置到目标位置
                    (int)_state.TargetY, 
                    _state.WindowWidth, 
                    _state.WindowHeight));
                System.Diagnostics.Debug.WriteLine("[WindowHostController] Window moved to target position");
                
                // 3. 标记首次显示完成，并记录时间（用于保护期）
                _isInitialShowComplete = true;
                _initialShowCompletedTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine("[WindowHostController] Initial show marked as complete");
                
                // 4. 更新状态到 Windowed
                var plan = _stateManager.CreatePlan(WindowState.Windowed, "Initial window shown");
                if (plan != null)
                {
                    _stateManager.CommitTransition(plan.TransitionId);
                    System.Diagnostics.Debug.WriteLine("[WindowHostController] State transition committed to Windowed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WindowHostController] WARNING: Failed to create state transition plan");
                }
                
                System.Diagnostics.Debug.WriteLine("[WindowHostController] RequestSlideIn completed successfully");
                
                // 5. 激活窗口（必须在最后）
                // 注意：Activate() 的行为特性：
                // - 这是首次创建窗口时唯一合法的显示方案
                // - 会触发系统内置的流畅窗口显示动画（DWM 动画）
                // - 内置了强制进入可显示区域的逻辑
                // - 必须在所有窗口配置（位置、大小、样式等）完成后最后调用
                // - 如果在配置过程中调用会导致闪现问题
                _window.Activate();
                System.Diagnostics.Debug.WriteLine("[WindowHostController] Window activated with built-in animation");
                
                // 6. 窗口激活后立即显示启动屏幕
                if (_window is global::Docked_AI.MainWindow mainWindow)
                {
                    System.Diagnostics.Debug.WriteLine("[WindowHostController] Showing splash screen immediately after activation");
                    mainWindow.ShowSplash();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHostController] ERROR in RequestSlideIn: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[WindowHostController] Stack trace: {ex.StackTrace}");
                
                // 重置标志以允许重试
                _animationStarted = false;
                throw;
            }
        }

        /// <summary>
        /// 切换窗口显示/隐藏状态
        /// 使用 StateManager.CreatePlan 统一管理状态转换
        /// 支持直接转换：Pinned/Maximized -> Hidden（内部自动执行组合副作用）
        /// </summary>
        public void ToggleWindow()
        {
            var currentState = _stateManager.CurrentState;
            WindowState targetState = currentState is WindowState.Hidden or WindowState.NotCreated
                ? WindowState.Windowed
                : WindowState.Hidden;

            _ = TryRequestTransition(targetState, "User toggled window", nameof(ToggleWindow));
        }

        private void InitializeWindow()
        {
            // 先获取窗口句柄
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            
            // 初始化时不设置位置，避免窗口在屏幕上闪现
            // 让 RequestSlideIn() 在首次显示时设置正确的位置和动画
            _layoutService.Refresh(_state);
            _window.AppWindow.IsShownInSwitchers = false;
            
            // 将窗口移到屏幕外，避免初始化时可见
            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                _state.ScreenWidth + 100,  // 屏幕外
                (int)_state.TargetY, 
                _state.WindowWidth, 
                _state.WindowHeight));

            // 配置标题栏（不设置背景，等启动屏幕完成后再设置）
            _titleBarService.ConfigureStandardWindow(_window);
            // 注意：不在这里设置亚克力背景，避免在启动屏幕前显示
            // 亚克力背景将在 ShowSplash() 完成后设置

            // 订阅事件
            _window.Activated += OnWindowActivated;
            _window.Activated += OnActivationChanged;
            _window.Closed += OnWindowClosed;
            _window.AppWindow.Changed += OnAppWindowChanged;
        }

        /// <summary>
        /// 切换固定模式
        /// 使用 StateManager.CreatePlan 统一管理状态转换
        /// 支持自动两步转换：Maximized -> Windowed -> Pinned
        /// </summary>
        public void TogglePinnedDock()
        {
            var currentState = _stateManager.CurrentState;
            switch (currentState)
            {
                case WindowState.Pinned:
                    _ = TryRequestTransition(WindowState.Windowed, "User toggled pinned dock", nameof(TogglePinnedDock));
                    return;
                case WindowState.Windowed:
                    _ = TryRequestTransition(WindowState.Pinned, "User toggled pinned dock", nameof(TogglePinnedDock));
                    return;
                case WindowState.Maximized:
                    _ = TransitionThroughWindowedAsync(WindowState.Pinned, "User toggled pinned dock from maximized");
                    return;
                default:
                    System.Diagnostics.Debug.WriteLine($"TogglePinnedDock not allowed from state: {currentState}");
                    return;
            }
        }

        /// <summary>
        /// 切换最大化/还原状态
        /// 使用 StateManager.CreatePlan 统一管理状态转换
        /// 支持自动两步转换：Pinned -> Windowed -> Maximized
        /// </summary>
        public void ToggleMaximize()
        {
            var currentState = _stateManager.CurrentState;
            switch (currentState)
            {
                case WindowState.Maximized:
                    _ = TryRequestTransition(WindowState.Windowed, "User toggled maximize", nameof(ToggleMaximize));
                    return;
                case WindowState.Windowed:
                    _ = TryRequestTransition(WindowState.Maximized, "User toggled maximize", nameof(ToggleMaximize));
                    return;
                case WindowState.Pinned:
                    _ = TransitionThroughWindowedAsync(WindowState.Maximized, "User toggled maximize from pinned");
                    return;
                default:
                    System.Diagnostics.Debug.WriteLine($"ToggleMaximize not allowed from state: {currentState}");
                    return;
            }
        }

        private bool TryRequestTransition(WindowState targetState, string reason, string operationName)
        {
            EnsureWindowHandle();

            if (_stateManager.IsTransitioning)
            {
                System.Diagnostics.Debug.WriteLine($"{operationName} blocked: state transition in progress");
                return false;
            }

            WindowState currentState = _stateManager.CurrentState;
            if (_stateManager.CreatePlan(targetState, reason) is not null)
            {
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"{operationName}: Failed to create transition plan: {currentState} -> {targetState}");
            return false;
        }

        /// <summary>
        /// 从 OS 窗口状态同步到内部状态
        /// 使用 QueueSyncEvent 排队外部同步事件
        /// 转换期间延迟同步，转换完成后处理最新事件
        /// </summary>
        /// <param name="osState">OS 报告的窗口状态</param>
        public void SyncFromOSWindowState(WindowState osState)
        {
            // 使用 StateManager 的 QueueSyncEvent 排队同步事件
            // 如果正在转换，事件会被延迟；否则立即同步
            _stateManager.QueueSyncEvent(osState);
        }

        /// <summary>
        /// 通过 Windowed 状态进行两步转换
        /// 用于处理 Pinned ↔ Maximized 之间的转换
        /// </summary>
        /// <param name="finalState">最终目标状态（Pinned 或 Maximized）</param>
        /// <param name="reason">转换原因</param>
        private async System.Threading.Tasks.Task TransitionThroughWindowedAsync(WindowState finalState, string reason)
        {
            var currentState = _stateManager.CurrentState;
            
            // 验证当前状态和目标状态
            if (currentState == WindowState.Windowed || finalState == WindowState.Windowed)
            {
                System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Invalid states (current={currentState}, final={finalState})");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: {currentState} -> Windowed -> {finalState}");

            // 第一步：转换到 Windowed
            if (!TryRequestTransition(WindowState.Windowed, $"{reason} (step 1: to Windowed)", nameof(TransitionThroughWindowedAsync)))
            {
                System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Failed to create plan for step 1");
                return;
            }

            if (!await WaitForCommittedStateAsync(WindowState.Windowed, TransitionThroughWindowedTimeout))
            {
                System.Diagnostics.Debug.WriteLine("TransitionThroughWindowed: Timeout waiting for step 1 to complete");
                return;
            }

            System.Diagnostics.Debug.WriteLine("TransitionThroughWindowed: Step 1 completed, starting step 2");
            await System.Threading.Tasks.Task.Delay(IntermediateTransitionDelay);

            if (TryRequestTransition(finalState, $"{reason} (step 2: to {finalState})", nameof(TransitionThroughWindowedAsync)))
            {
                System.Diagnostics.Debug.WriteLine("TransitionThroughWindowed: Step 2 plan created, transition will complete automatically");
            }
        }

        private async System.Threading.Tasks.Task<bool> WaitForCommittedStateAsync(WindowState expectedState, TimeSpan timeout)
        {
            DateTime startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                if (_stateManager.CommittedState == expectedState && !_stateManager.IsTransitioning)
                {
                    return true;
                }

                await System.Threading.Tasks.Task.Delay(TransitionPollInterval);
            }

            return false;
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            // 首次显示时，忽略激活事件（由 RequestSlideIn 控制）
            if (!_animationStarted)
            {
                System.Diagnostics.Debug.WriteLine("OnWindowActivated: Ignoring activation before RequestSlideIn");
                return;
            }

            // 后续的激活事件正常处理
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                System.Diagnostics.Debug.WriteLine("OnWindowActivated: Window activated");
            }
        }

        private void OnActivationChanged(object sender, WindowActivatedEventArgs args)
        {
            // 防重入检查
            if (_stateManager.IsTransitioning)
            {
                System.Diagnostics.Debug.WriteLine("[OnActivationChanged] Blocked: state transition in progress");
                return;
            }

            var currentState = _stateManager.CurrentState;

            // 首次显示后的保护期：防止动画完成后立即因失去焦点而隐藏
            if (_isInitialShowComplete)
            {
                var timeSinceInitialShow = DateTime.Now - _initialShowCompletedTime;
                if (timeSinceInitialShow < InitialShowProtectionPeriod)
                {
                    System.Diagnostics.Debug.WriteLine($"[OnActivationChanged] Within protection period ({timeSinceInitialShow.TotalMilliseconds}ms < {InitialShowProtectionPeriod.TotalMilliseconds}ms), ignoring deactivation");
                    return;
                }
            }

            // 窗口失去焦点且未固定时自动隐藏
            if (args.WindowActivationState == WindowActivationState.Deactivated &&
                currentState != WindowState.Hidden &&
                currentState != WindowState.NotCreated &&
                currentState != WindowState.Pinned)
            {
                System.Diagnostics.Debug.WriteLine("[OnActivationChanged] Window deactivated, requesting hide");
                _ = TryRequestTransition(WindowState.Hidden, "Window deactivated", nameof(OnActivationChanged));
            }
        }

        private void StartInitialSlideIn()
        {
            _layoutService.PrepareForShow(_state);
            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)_state.CurrentX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
            _animationController.StartShow();
            
            // 初始化完成后，将状态从 NotCreated 转换到 Windowed
            var plan = _stateManager.CreatePlan(WindowState.Windowed, "Initial window shown");
            if (plan != null)
            {
                // 立即提交状态（初始动画不需要等待）
                _stateManager.CommitTransition(plan.TransitionId);
            }
        }

        private void StartShowAnimation()
        {
            _window.AppWindow.IsShownInSwitchers = false;
            _backdropService.EnsureAcrylicBackdrop(_window);
            _titleBarService.ConfigureStandardWindow(_window);
            MoveWindowToStandardDock(prepareForShow: true);
            _animationController.StartShow();
            
            // 注意：ActivateAndFocusWindow() 必须在最后调用
            // 因为它内部调用 Activate()，会触发窗口显示动画
            ActivateAndFocusWindow();
        }

        private void StartHideAnimation()
        {
            RemoveAppBar();
            SetTopMost(false);

            _layoutService.Refresh(_state);
            _state.CurrentX = _state.TargetX;
            _state.CurrentY = _state.TargetY;

            _layoutService.PrepareForHide(_state);
            _state.TargetX = _state.ScreenWidth;
            _state.TargetY = _state.WorkArea.Top + _state.Margin;
            _state.CurrentY = _state.TargetY;
            _animationController.StartHide();
        }

        private void ApplyPinnedMode()
        {
            _window.AppWindow.IsShownInSwitchers = false;
            ApplyPinnedWindowStyle();
            ApplyPinnedBounds();
            _backdropService.EnsureMicaBackdrop(_window);
            
            // 注意：ActivateAndFocusWindow() 必须在最后调用
            // 因为它内部调用 Activate()，会触发窗口显示动画
            ActivateAndFocusWindow();
        }

        private void RestoreStandardMode()
        {
            RemoveAppBar();
            RestoreStandardWindowStyle();
            _titleBarService.ConfigureStandardWindow(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);
            MoveWindowToStandardDock(prepareForShow: false);
            SetTopMost(false);
        }

        private void ActivateAndFocusWindow()
        {
            // 注意：Activate() 的行为特性：
            // - 这是首次创建窗口时唯一合法的显示方案
            // - 会触发系统内置的流畅窗口显示动画（DWM 动画）
            // - 内置了强制进入可显示区域的逻辑
            // - 必须在所有窗口配置（位置、大小、样式等）完成后最后调用
            // - 如果在配置过程中调用会导致闪现问题
            _window.Activate();
            Tray.WindowHelper.SetForegroundWindow(_window);
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange || _isApplyingPinnedBounds)
            {
                return;
            }

            int availableWidth = _state.WorkArea.Right - _state.WorkArea.Left - (_state.Margin * 2);
            if (sender.Size.Width > 0)
            {
                _state.WindowWidth = Math.Max(_state.MinWindowWidth, Math.Min(availableWidth, sender.Size.Width));
            }

            var currentState = _stateManager.CurrentState;
            if (currentState == WindowState.Pinned)
            {
                ApplyPinnedBounds();
            }
        }

        private void MoveWindowToStandardDock(bool prepareForShow)
        {
            if (prepareForShow)
            {
                _layoutService.PrepareForShow(_state);
            }
            else
            {
                _layoutService.Refresh(_state);
                _state.CurrentX = _state.TargetX;
                _state.CurrentY = _state.TargetY;
            }

            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)_state.CurrentX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
        }

        private void ApplyPinnedBounds()
        {
            _layoutService.Refresh(_state);
            RegisterAppBarIfNeeded();

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            int desiredWidth = _state.WindowWidth;

            appBarData.uEdge = VisibilityWin32Api.ABE_RIGHT;
            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_QUERYPOS, ref appBarData);

            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_SETPOS, ref appBarData);

            int width = Math.Max(_state.MinWindowWidth, appBarData.rc.Right - appBarData.rc.Left);
            int height = Math.Max(1, appBarData.rc.Bottom - appBarData.rc.Top);

            _state.WindowWidth = width;
            _state.WindowHeight = height;
            _state.TargetX = appBarData.rc.Left;
            _state.TargetY = appBarData.rc.Top;
            _state.CurrentX = _state.TargetX;
            _state.CurrentY = _state.TargetY;

            _isApplyingPinnedBounds = true;
            try
            {
                ApplyPinnedWindowFrame(appBarData.rc);
            }
            finally
            {
                _isApplyingPinnedBounds = false;
            }
        }

        private void SetTopMost(bool isTopMost)
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = VisibilityWin32Api.SetWindowPos(
                _hwnd,
                isTopMost ? VisibilityWin32Api.HWND_TOPMOST : VisibilityWin32Api.HWND_NOTOPMOST,
                _state.TargetX,
                (int)_state.TargetY,
                _state.WindowWidth,
                _state.WindowHeight,
                VisibilityWin32Api.SWP_SHOWWINDOW);
        }

        private void EnsureWindowHandle()
        {
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
                TrySubclassWindow();
            }
        }

        private void TrySubclassWindow()
        {
            if (_isWindowSubclassed || _hwnd == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine($"TrySubclassWindow: Skipped (already subclassed={_isWindowSubclassed}, hwnd={_hwnd})");
                return;
            }

            IntPtr newWindowProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_windowProcDelegate);
            _originalWindowProc = VisibilityWin32Api.SetWindowProc(_hwnd, newWindowProc);
            _isWindowSubclassed = _originalWindowProc != IntPtr.Zero;
            
            System.Diagnostics.Debug.WriteLine($"TrySubclassWindow: Subclassed={_isWindowSubclassed}, OriginalProc={_originalWindowProc}, NewProc={newWindowProc}");
        }

        private void ApplyPinnedWindowStyle()
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            // 先配置 AppWindow
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                presenter.IsResizable = false; // 完全禁用调整大小以移除边框
                presenter.IsAlwaysOnTop = true;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            IntPtr currentStyle = AppearanceWin32Api.GetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE);
            IntPtr currentExtendedStyle = AppearanceWin32Api.GetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE);
            if (!_hasCapturedBaseWindowStyle)
            {
                _baseWindowStyle = currentStyle;
                _hasCapturedBaseWindowStyle = true;
            }
            if (!_hasCapturedBaseExtendedWindowStyle)
            {
                _baseExtendedWindowStyle = currentExtendedStyle;
                _hasCapturedBaseExtendedWindowStyle = true;
            }

            int style = currentStyle.ToInt32();
            // 移除所有边框和标题栏相关样式
            style &= ~AppearanceWin32Api.WS_OVERLAPPEDWINDOW;
            style &= ~AppearanceWin32Api.WS_CAPTION;
            style &= ~AppearanceWin32Api.WS_SYSMENU;
            style &= ~AppearanceWin32Api.WS_THICKFRAME;
            style &= ~AppearanceWin32Api.WS_BORDER;
            style &= ~AppearanceWin32Api.WS_DLGFRAME;
            // 只保留 POPUP 和 VISIBLE
            style |= AppearanceWin32Api.WS_POPUP;
            style |= AppearanceWin32Api.WS_VISIBLE;

            int extendedStyle = currentExtendedStyle.ToInt32();
            extendedStyle &= ~AppearanceWin32Api.WS_EX_DLGMODALFRAME;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_WINDOWEDGE;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_CLIENTEDGE;
            extendedStyle &= ~AppearanceWin32Api.WS_EX_STATICEDGE;

            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE, new IntPtr(style));
            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE, new IntPtr(extendedStyle));

            // 关键：使用 DwmExtendFrameIntoClientArea 扩展框架到客户区
            AppearanceWin32Api.MARGINS margins = new AppearanceWin32Api.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            _ = AppearanceWin32Api.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // DWM 属性设置
            int cornerPreference = AppearanceWin32Api.DWMWCP_DONOTROUND;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPreference,
                sizeof(int));

            // 移除边框颜色
            int borderColor = AppearanceWin32Api.DWMWA_COLOR_NONE;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_BORDER_COLOR,
                ref borderColor,
                sizeof(int));

            // 设置标题栏颜色为完全透明（ARGB: 0x01000000 - 几乎透明的黑色，让 Acrylic 透过）
            int captionColor = 0x01000000;
            _ = AppearanceWin32Api.DwmSetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_CAPTION_COLOR,
                ref captionColor,
                sizeof(int));

            RefreshWindowFrame();
            
            System.Diagnostics.Debug.WriteLine($"ApplyPinnedWindowStyle: All styles and DWM attributes applied");
        }

        private void RestoreStandardWindowStyle()
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero || !_hasCapturedBaseWindowStyle)
            {
                return;
            }

            _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_STYLE, _baseWindowStyle);
            if (_hasCapturedBaseExtendedWindowStyle)
            {
                _ = AppearanceWin32Api.SetWindowLongPtr(_hwnd, AppearanceWin32Api.GWL_EXSTYLE, _baseExtendedWindowStyle);
            }
            RefreshWindowFrame();
        }

        private void RefreshWindowFrame()
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = VisibilityWin32Api.SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                VisibilityWin32Api.SWP_NOSIZE |
                VisibilityWin32Api.SWP_NOMOVE |
                VisibilityWin32Api.SWP_NOZORDER |
                VisibilityWin32Api.SWP_NOACTIVATE |
                VisibilityWin32Api.SWP_FRAMECHANGED);
        }

        private void ApplyPinnedWindowFrame(PlacementWin32Api.RECT approvedRect)
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyWindowRect(approvedRect);

            if (TryGetExtendedFrameBounds(out PlacementWin32Api.RECT actualBounds))
            {
                PlacementWin32Api.RECT correctedRect = new()
                {
                    Left = approvedRect.Left + (approvedRect.Left - actualBounds.Left),
                    Top = approvedRect.Top + (approvedRect.Top - actualBounds.Top),
                    Right = approvedRect.Right + (approvedRect.Right - actualBounds.Right),
                    Bottom = approvedRect.Bottom + (approvedRect.Bottom - actualBounds.Bottom)
                };

                ApplyWindowRect(correctedRect);
                approvedRect = correctedRect;
            }

            _state.TargetX = approvedRect.Left;
            _state.TargetY = approvedRect.Top;
            _state.CurrentX = approvedRect.Left;
            _state.CurrentY = approvedRect.Top;
            _state.WindowWidth = Math.Max(_state.MinWindowWidth, approvedRect.Right - approvedRect.Left);
            _state.WindowHeight = Math.Max(1, approvedRect.Bottom - approvedRect.Top);
        }

        private void ApplyWindowRect(PlacementWin32Api.RECT rect)
        {
            int width = Math.Max(_state.MinWindowWidth, rect.Right - rect.Left);
            int height = Math.Max(1, rect.Bottom - rect.Top);

            _ = VisibilityWin32Api.SetWindowPos(
                _hwnd,
                VisibilityWin32Api.HWND_TOPMOST,
                rect.Left,
                rect.Top,
                width,
                height,
                VisibilityWin32Api.SWP_SHOWWINDOW);
        }

        private bool TryGetExtendedFrameBounds(out PlacementWin32Api.RECT bounds)
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                bounds = default;
                return false;
            }

            int hr = AppearanceWin32Api.DwmGetWindowAttribute(
                _hwnd,
                AppearanceWin32Api.DWMWA_EXTENDED_FRAME_BOUNDS,
                out bounds,
                System.Runtime.InteropServices.Marshal.SizeOf<PlacementWin32Api.RECT>());

            return hr >= 0;
        }

        private void RegisterAppBarIfNeeded()
        {
            if (_isAppBarRegistered)
            {
                return;
            }

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_NEW, ref appBarData);
            _isAppBarRegistered = true;
        }

        private void RemoveAppBar()
        {
            if (!_isAppBarRegistered)
            {
                return;
            }

            VisibilityWin32Api.APPBARDATA appBarData = CreateAppBarData();
            _ = VisibilityWin32Api.SHAppBarMessage(VisibilityWin32Api.ABM_REMOVE, ref appBarData);
            _isAppBarRegistered = false;
        }

        private VisibilityWin32Api.APPBARDATA CreateAppBarData()
        {
            EnsureWindowHandle();
            return new VisibilityWin32Api.APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<VisibilityWin32Api.APPBARDATA>(),
                hWnd = _hwnd,
                uCallbackMessage = _appBarMessageId
            };
        }

        /// <summary>
        /// 状态变化事件处理器，使用命令模式协调所有状态转换
        /// </summary>
        private async void OnWindowStateChanged(object? sender, StateChangedEventArgs args)
        {
            // 注意：此方法在 StateChanged 事件触发时被调用
            // CreatePlan 已经在调用方（如 ToggleWindow）中被调用
            // 这里只需要执行副作用，不需要再次调用 CreatePlan
            
            // 从事件参数中获取 transitionId
            int transitionId = args.TransitionId;

            try
            {
                // 根据新状态执行相应的窗口操作（带超时保护）
                // 支持组合副作用：Maximized/Pinned -> Hidden 内部自动执行多步操作
                System.Threading.Tasks.Task animationTask = (args.PreviousState, args.CurrentState) switch
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

                    _ => System.Threading.Tasks.Task.CompletedTask
                };

                // 等待动画完成或超时
                var completedTask = await System.Threading.Tasks.Task.WhenAny(animationTask, System.Threading.Tasks.Task.Delay(_animationTimeoutMs));

                if (completedTask != animationTask)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Animation timeout ({_animationTimeoutMs}ms) for state {args.CurrentState}");
                    // 超时视为失败，回滚状态
                    _stateManager.RollbackTransition(transitionId, "Animation timeout");
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
            }
        }

        /// <summary>
        /// 执行组合副作用（按顺序执行多个异步操作，记录子转换）
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteCompositeAsync(int transitionId, params StateTransition[] subTransitions)
        {
            foreach (var subTransition in subTransitions)
            {
                // 记录子转换到历史
                _stateManager.RecordSubTransition(transitionId, subTransition);

                // 执行子转换的副作用
                System.Threading.Tasks.Task subTask = (subTransition.FromState, subTransition.ToState) switch
                {
                    (WindowState.Maximized, WindowState.Windowed) => RestoreFromMaximizedModeAsync(),
                    (WindowState.Pinned, WindowState.Windowed) => RestoreFromPinnedModeAsync(),
                    (WindowState.Windowed, WindowState.Hidden) => ExecuteHideAnimationAsync(),
                    _ => System.Threading.Tasks.Task.CompletedTask
                };

                await subTask;
            }
        }

        private async System.Threading.Tasks.Task ExecuteHideAnimationAsync()
        {
            StartHideAnimation();
            await System.Threading.Tasks.Task.Delay(SlideAnimationDelay);
        }

        private async System.Threading.Tasks.Task ExecuteShowAnimationAsync()
        {
            StartShowAnimation();
            await System.Threading.Tasks.Task.Delay(SlideAnimationDelay);
        }

        private async System.Threading.Tasks.Task ApplyPinnedModeAsync()
        {
            ApplyPinnedMode();
            await System.Threading.Tasks.Task.Delay(PinnedModeDelay);
        }

        private async System.Threading.Tasks.Task ApplyMaximizedModeAsync()
        {
            if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            await System.Threading.Tasks.Task.Delay(MaximizedModeDelay);
        }

        private async System.Threading.Tasks.Task RestoreFromPinnedModeAsync()
        {
            RestoreStandardMode();
            await System.Threading.Tasks.Task.Delay(PinnedModeDelay);
        }

        private async System.Threading.Tasks.Task RestoreFromMaximizedModeAsync()
        {
            if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Restore();
            }

            await System.Threading.Tasks.Task.Delay(MaximizedModeDelay);
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            _window.Activated -= OnWindowActivated;
            _window.Activated -= OnActivationChanged;
            _window.Closed -= OnWindowClosed;
            _window.AppWindow.Changed -= OnAppWindowChanged;
            _stateManager.StateChanged -= OnWindowStateChanged;
            _viewModel.UnsubscribeFromStateManager(_stateManager);
            _stateManager.Dispose();
            _backdropService.Dispose();
            RemoveAppBar();
        }

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var currentState = _stateManager.CurrentState;
            
            // 添加调试输出，验证 WindowProc 是否被调用
            if (msg == VisibilityWin32Api.WM_NCCALCSIZE || 
                msg == VisibilityWin32Api.WM_NCPAINT || 
                msg == VisibilityWin32Api.WM_NCACTIVATE)
            {
                System.Diagnostics.Debug.WriteLine($"WindowProc: msg=0x{msg:X}, currentState={currentState}");
            }
            
            if (currentState == WindowState.Pinned)
            {
                if (msg == VisibilityWin32Api.WM_NCCALCSIZE)
                {
                    System.Diagnostics.Debug.WriteLine($"WM_NCCALCSIZE: wParam={wParam}, lParam={lParam}");
                    
                    if (wParam != IntPtr.Zero && lParam != IntPtr.Zero)
                    {
                        // 当 wParam 为 TRUE 时，lParam 指向 NCCALCSIZE_PARAMS 结构
                        // 返回 0 表示客户区占据整个窗口区域
                        System.Diagnostics.Debug.WriteLine("WM_NCCALCSIZE: Returning 0 to make client area = window area");
                        return IntPtr.Zero;
                    }
                    return IntPtr.Zero;
                }

                if (msg == VisibilityWin32Api.WM_NCPAINT)
                {
                    System.Diagnostics.Debug.WriteLine("WM_NCPAINT: Blocking non-client paint");
                    return IntPtr.Zero;
                }

                if (msg == VisibilityWin32Api.WM_NCACTIVATE)
                {
                    System.Diagnostics.Debug.WriteLine("WM_NCACTIVATE: Returning 1 without drawing");
                    return new IntPtr(1);
                }
            }

            return VisibilityWin32Api.CallWindowProc(_originalWindowProc, hWnd, msg, wParam, lParam);
        }
    }
}

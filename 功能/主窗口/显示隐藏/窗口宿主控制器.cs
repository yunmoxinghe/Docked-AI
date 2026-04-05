using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Appearance;
using Docked_AI.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using System;

namespace Docked_AI.Features.MainWindow.Visibility
{
    internal sealed class WindowHostController
    {
        private readonly Window _window;
        private readonly MainWindowViewModel _viewModel;
        private readonly WindowLayoutService _layoutService;
        private readonly WindowLayoutState _state;
        private readonly TitleBarService _titleBarService;
        private readonly BackdropService _backdropService;
        private readonly SlideAnimationController _animationController;
        private readonly WindowStateManager _stateManager;
        private readonly int _animationTimeoutMs;

        private bool _animationStarted;
        private bool _isAppBarRegistered;
        private bool _isApplyingPinnedBounds;
        private bool _hasCapturedBaseWindowStyle;
        private bool _hasCapturedBaseExtendedWindowStyle;
        private bool _isWindowSubclassed;
        private IntPtr _hwnd;
        private readonly uint _appBarMessageId;
        private IntPtr _baseWindowStyle;
        private IntPtr _baseExtendedWindowStyle;
        private IntPtr _originalWindowProc;
        private readonly VisibilityWin32Api.WindowProc _windowProcDelegate;

        private const int DefaultAnimationTimeoutMs = 2000;

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
        /// 切换窗口显示/隐藏状态
        /// 使用 StateManager.CreatePlan 统一管理状态转换
        /// 支持直接转换：Pinned/Maximized -> Hidden（内部自动执行组合副作用）
        /// </summary>
        public void ToggleWindow()
        {
            EnsureWindowHandle();

            // 防重入检查
            if (_stateManager.IsTransitioning)
            {
                System.Diagnostics.Debug.WriteLine("ToggleWindow blocked: state transition in progress");
                return;
            }

            var currentState = _stateManager.CurrentState;
            WindowState targetState;

            // 根据当前状态决定目标状态
            if (currentState == WindowState.Hidden || currentState == WindowState.NotCreated)
            {
                // 隐藏 -> 窗口化
                targetState = WindowState.Windowed;
            }
            else
            {
                // 其他状态 -> 隐藏（支持直接转换：Pinned/Maximized -> Hidden）
                targetState = WindowState.Hidden;
            }

            // 使用 StateManager 创建转换计划
            var plan = _stateManager.CreatePlan(targetState, "User toggled window");
            if (plan == null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create transition plan: {currentState} -> {targetState}");
                return;
            }

            // 计划已创建，OnWindowStateChanged 会自动执行副作用
        }

        private void InitializeWindow()
        {
            _titleBarService.ConfigureStandardWindow(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);

            _layoutService.Refresh(_state);
            _state.CurrentX = _state.ScreenWidth;
            _state.CurrentY = _state.TargetY;
            _window.AppWindow.IsShownInSwitchers = false;
            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)_state.CurrentX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));

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
            EnsureWindowHandle();

            // 防重入检查
            if (_stateManager.IsTransitioning)
            {
                System.Diagnostics.Debug.WriteLine("TogglePinnedDock blocked: state transition in progress");
                return;
            }

            var currentState = _stateManager.CurrentState;
            WindowState targetState;

            // 根据当前状态决定目标状态
            if (currentState == WindowState.Pinned)
            {
                // 固定 -> 窗口化
                targetState = WindowState.Windowed;
            }
            else if (currentState == WindowState.Windowed)
            {
                // 窗口化 -> 固定
                targetState = WindowState.Pinned;
            }
            else if (currentState == WindowState.Maximized)
            {
                // 最大化 -> 窗口化 -> 固定（自动两步转换）
                TransitionThroughWindowed(WindowState.Pinned, "User toggled pinned dock from maximized");
                return;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"TogglePinnedDock not allowed from state: {currentState}");
                return;
            }

            // 使用 StateManager 创建转换计划
            var plan = _stateManager.CreatePlan(targetState, "User toggled pinned dock");
            if (plan == null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create transition plan: {currentState} -> {targetState}");
                return;
            }

            // 计划已创建，OnWindowStateChanged 会自动执行副作用
        }

        /// <summary>
        /// 切换最大化/还原状态
        /// 使用 StateManager.CreatePlan 统一管理状态转换
        /// 支持自动两步转换：Pinned -> Windowed -> Maximized
        /// </summary>
        public void ToggleMaximize()
        {
            EnsureWindowHandle();

            // 防重入检查
            if (_stateManager.IsTransitioning)
            {
                System.Diagnostics.Debug.WriteLine("ToggleMaximize blocked: state transition in progress");
                return;
            }

            var currentState = _stateManager.CurrentState;
            WindowState targetState;

            // 根据当前状态决定目标状态
            if (currentState == WindowState.Maximized)
            {
                // 最大化 -> 窗口化
                targetState = WindowState.Windowed;
            }
            else if (currentState == WindowState.Windowed)
            {
                // 窗口化 -> 最大化
                targetState = WindowState.Maximized;
            }
            else if (currentState == WindowState.Pinned)
            {
                // 固定 -> 窗口化 -> 最大化（自动两步转换）
                TransitionThroughWindowed(WindowState.Maximized, "User toggled maximize from pinned");
                return;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ToggleMaximize not allowed from state: {currentState}");
                return;
            }

            // 使用 StateManager 创建转换计划
            var plan = _stateManager.CreatePlan(targetState, "User toggled maximize");
            if (plan == null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create transition plan: {currentState} -> {targetState}");
                return;
            }

            // 计划已创建，OnWindowStateChanged 会自动执行副作用
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
        private async void TransitionThroughWindowed(WindowState finalState, string reason)
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
            var plan1 = _stateManager.CreatePlan(WindowState.Windowed, $"{reason} (step 1: to Windowed)");
            if (plan1 == null)
            {
                System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Failed to create plan for step 1");
                return;
            }

            // 等待第一步完成（轮询 CommittedState）
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(5);
            
            while (DateTime.Now - startTime < timeout)
            {
                // 检查状态是否已经提交为 Windowed
                if (_stateManager.CommittedState == WindowState.Windowed && !_stateManager.IsTransitioning)
                {
                    System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Step 1 completed, starting step 2");
                    
                    // 等待一小段时间，确保 UI 稳定
                    await System.Threading.Tasks.Task.Delay(100);
                    
                    // 第二步：从 Windowed 转换到最终状态
                    var plan2 = _stateManager.CreatePlan(finalState, $"{reason} (step 2: to {finalState})");
                    if (plan2 == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Failed to create plan for step 2");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Step 2 plan created, transition will complete automatically");
                    return;
                }
                
                // 等待 50ms 后再次检查
                await System.Threading.Tasks.Task.Delay(50);
            }

            System.Diagnostics.Debug.WriteLine($"TransitionThroughWindowed: Timeout waiting for step 1 to complete");
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated || _animationStarted)
            {
                return;
            }

            _animationStarted = true;
            _window.Activated -= OnWindowActivated;

            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            _titleBarService.ConfigureStandardWindow(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);
            StartInitialSlideIn();
        }

        private async void OnActivationChanged(object sender, WindowActivatedEventArgs args)
        {
            // 防重入检查
            if (_stateManager.IsTransitioning)
            {
                return;
            }

            var currentState = _stateManager.CurrentState;

            // 窗口失去焦点且未固定时自动隐藏
            if (args.WindowActivationState == WindowActivationState.Deactivated &&
                currentState != WindowState.Hidden &&
                currentState != WindowState.NotCreated &&
                currentState != WindowState.Pinned)
            {
                // 使用 StateManager 创建转换计划
                var plan = _stateManager.CreatePlan(WindowState.Hidden, "Window deactivated");
                if (plan != null)
                {
                    // 计划已创建，OnWindowStateChanged 会自动执行副作用
                }
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

        private void ShowWindow()
        {
            _window.AppWindow.IsShownInSwitchers = false;
            _backdropService.EnsureAcrylicBackdrop(_window);

            var currentState = _stateManager.CurrentState;

            if (currentState == WindowState.Pinned)
            {
                ShowPinnedDock();
                return;
            }

            _titleBarService.ConfigureStandardWindow(_window);

            MoveWindowToStandardDock(prepareForShow: true);

            _window.Activate();
            Tray.WindowHelper.SetForegroundWindow(_window);
            _animationController.StartShow();
        }

        private void HideWindow()
        {
            _ = HideWindowAsync();
        }

        private async System.Threading.Tasks.Task HideWindowAsync()
        {
            bool wasMaximized = false;
            if (_window.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped)
            {
                var presenter = _window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                {
                    wasMaximized = true;
                    presenter.Restore();
                }
            }

            // 如果窗口是最大化状态，等待还原动画完成
            if (wasMaximized)
            {
                await System.Threading.Tasks.Task.Delay(500);
            }

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

        private void ShowPinnedDock()
        {
            _window.AppWindow.IsShownInSwitchers = false;
            
            ApplyPinnedWindowStyle();
            
            ApplyPinnedBounds();
            
            _backdropService.EnsureMicaBackdrop(_window);

            _window.Activate();
            Tray.WindowHelper.SetForegroundWindow(_window);
        }

        private void RestoreStandardDock()
        {
            RemoveAppBar();
            RestoreStandardWindowStyle();
            _titleBarService.ConfigureStandardWindow(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);
            MoveWindowToStandardDock(prepareForShow: false);
            SetTopMost(false);
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
                return;
            }

            IntPtr newWindowProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_windowProcDelegate);
            _originalWindowProc = VisibilityWin32Api.SetWindowProc(_hwnd, newWindowProc);
            _isWindowSubclassed = _originalWindowProc != IntPtr.Zero;
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
            // 执行隐藏动画
            _layoutService.Refresh(_state);
            _state.CurrentX = _state.TargetX;
            _state.CurrentY = _state.TargetY;

            _layoutService.PrepareForHide(_state);
            _state.TargetX = _state.ScreenWidth;
            _state.TargetY = _state.WorkArea.Top + _state.Margin;
            _state.CurrentY = _state.TargetY;
            _animationController.StartHide();
            await System.Threading.Tasks.Task.Delay(300); // 等待动画完成
        }

        private async System.Threading.Tasks.Task ExecuteShowAnimationAsync()
        {
            // 执行显示动画
            _window.AppWindow.IsShownInSwitchers = false;
            _backdropService.EnsureAcrylicBackdrop(_window);
            _titleBarService.ConfigureStandardWindow(_window);

            MoveWindowToStandardDock(prepareForShow: true);

            _window.Activate();
            Tray.WindowHelper.SetForegroundWindow(_window);
            _animationController.StartShow();
            await System.Threading.Tasks.Task.Delay(300); // 等待动画完成
        }

        private async System.Threading.Tasks.Task ApplyPinnedModeAsync()
        {
            // 应用固定模式（可能包含动画）
            _window.AppWindow.IsShownInSwitchers = false;

            ApplyPinnedWindowStyle();

            ApplyPinnedBounds();

            _backdropService.EnsureMicaBackdrop(_window);

            _window.Activate();
            Tray.WindowHelper.SetForegroundWindow(_window);
            await System.Threading.Tasks.Task.Delay(100); // 等待样式应用完成
        }

        private async System.Threading.Tasks.Task ApplyMaximizedModeAsync()
        {
            // 应用最大化模式
            if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
            await System.Threading.Tasks.Task.Delay(200); // 等待最大化动画完成
        }

        private async System.Threading.Tasks.Task RestoreFromPinnedModeAsync()
        {
            // 从固定模式还原
            RemoveAppBar();
            RestoreStandardWindowStyle();
            _titleBarService.ConfigureStandardWindow(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);
            MoveWindowToStandardDock(prepareForShow: false);
            SetTopMost(false);
            await System.Threading.Tasks.Task.Delay(100); // 等待样式还原完成
        }

        private async System.Threading.Tasks.Task RestoreFromMaximizedModeAsync()
        {
            // 从最大化还原
            if (_window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Restore();
            }
            await System.Threading.Tasks.Task.Delay(200); // 等待还原动画完成
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

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var currentState = _stateManager.CurrentState;
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

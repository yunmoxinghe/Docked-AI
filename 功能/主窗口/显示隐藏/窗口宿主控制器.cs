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
        private readonly Win32WindowApi.WindowProc _windowProcDelegate;

        public WindowHostController(Window window, MainWindowViewModel viewModel)
        {
            _window = window;
            _viewModel = viewModel;
            _layoutService = new WindowLayoutService();
            _state = _layoutService.CreateInitialState();
            _titleBarService = new TitleBarService();
            _backdropService = new BackdropService();
            _animationController = new SlideAnimationController(_window, _state);
            _appBarMessageId = Win32WindowApi.RegisterWindowMessage("DockedAI_AppBarMessage");
            _windowProcDelegate = WindowProc;

            InitializeWindow();
        }

        public void ToggleWindow()
        {
            EnsureWindowHandle();

            if (_viewModel.IsWindowVisible)
            {
                // 如果窗口处于固定状态，先取消固定再隐藏
                if (_viewModel.IsDockPinned)
                {
                    _viewModel.SetDockPinned(false);
                    RestoreStandardDock();
                }
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
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

        public void TogglePinnedDock()
        {
            EnsureWindowHandle();

            bool shouldPin = !_viewModel.IsDockPinned;
            _viewModel.SetDockPinned(shouldPin);

            if (shouldPin)
            {
                ShowPinnedDock();
                return;
            }

            RestoreStandardDock();
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
            if (args.WindowActivationState == WindowActivationState.Deactivated &&
                _viewModel.IsWindowVisible &&
                !_viewModel.IsDockPinned)
            {
                await HideWindowAsync();
            }
        }

        private void StartInitialSlideIn()
        {
            _layoutService.PrepareForShow(_state);
            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)_state.CurrentX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
            _animationController.StartShow();
        }

        private void ShowWindow()
        {
            _viewModel.MarkVisible();
            _window.AppWindow.IsShownInSwitchers = false;
            _backdropService.EnsureAcrylicBackdrop(_window);

            if (_viewModel.IsDockPinned)
            {
                ShowPinnedDock();
                return;
            }

            _titleBarService.ConfigureStandardWindow(_window);

            MoveWindowToStandardDock(prepareForShow: true);

            _window.Activate();
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

            _viewModel.MarkHidden();
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

            if (_viewModel.IsDockPinned && _viewModel.IsWindowVisible)
            {
                ApplyPinnedBounds();
            }
        }

        private void ShowPinnedDock()
        {
            _viewModel.MarkVisible();
            _window.AppWindow.IsShownInSwitchers = false;
            
            ApplyPinnedWindowStyle();
            
            ApplyPinnedBounds();
            
            _backdropService.EnsureMicaBackdrop(_window);

            _window.Activate();
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

            Win32WindowApi.APPBARDATA appBarData = CreateAppBarData();
            int desiredWidth = _state.WindowWidth;

            appBarData.uEdge = Win32WindowApi.ABE_RIGHT;
            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = Win32WindowApi.SHAppBarMessage(Win32WindowApi.ABM_QUERYPOS, ref appBarData);

            appBarData.rc.Top = _state.WorkArea.Top;
            appBarData.rc.Bottom = _state.WorkArea.Bottom;
            appBarData.rc.Right = _state.WorkArea.Right;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = Win32WindowApi.SHAppBarMessage(Win32WindowApi.ABM_SETPOS, ref appBarData);

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

            _ = Win32WindowApi.SetWindowPos(
                _hwnd,
                isTopMost ? Win32WindowApi.HWND_TOPMOST : Win32WindowApi.HWND_NOTOPMOST,
                _state.TargetX,
                (int)_state.TargetY,
                _state.WindowWidth,
                _state.WindowHeight,
                Win32WindowApi.SWP_SHOWWINDOW);
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
            _originalWindowProc = Win32WindowApi.SetWindowLongPtr(_hwnd, Win32WindowApi.GWLP_WNDPROC, newWindowProc);
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

            IntPtr currentStyle = Win32WindowApi.GetWindowLongPtr(_hwnd, Win32WindowApi.GWL_STYLE);
            IntPtr currentExtendedStyle = Win32WindowApi.GetWindowLongPtr(_hwnd, Win32WindowApi.GWL_EXSTYLE);
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
            style &= ~Win32WindowApi.WS_OVERLAPPEDWINDOW;
            style &= ~Win32WindowApi.WS_CAPTION;
            style &= ~Win32WindowApi.WS_SYSMENU;
            style &= ~Win32WindowApi.WS_THICKFRAME;
            style &= ~Win32WindowApi.WS_BORDER;
            style &= ~Win32WindowApi.WS_DLGFRAME;
            // 只保留 POPUP 和 VISIBLE
            style |= Win32WindowApi.WS_POPUP;
            style |= Win32WindowApi.WS_VISIBLE;

            int extendedStyle = currentExtendedStyle.ToInt32();
            extendedStyle &= ~Win32WindowApi.WS_EX_DLGMODALFRAME;
            extendedStyle &= ~Win32WindowApi.WS_EX_WINDOWEDGE;
            extendedStyle &= ~Win32WindowApi.WS_EX_CLIENTEDGE;
            extendedStyle &= ~Win32WindowApi.WS_EX_STATICEDGE;

            _ = Win32WindowApi.SetWindowLongPtr(_hwnd, Win32WindowApi.GWL_STYLE, new IntPtr(style));
            _ = Win32WindowApi.SetWindowLongPtr(_hwnd, Win32WindowApi.GWL_EXSTYLE, new IntPtr(extendedStyle));

            // 关键：使用 DwmExtendFrameIntoClientArea 扩展框架到客户区
            Win32WindowApi.MARGINS margins = new Win32WindowApi.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            _ = Win32WindowApi.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // DWM 属性设置
            int cornerPreference = Win32WindowApi.DWMWCP_DONOTROUND;
            _ = Win32WindowApi.DwmSetWindowAttribute(
                _hwnd,
                Win32WindowApi.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPreference,
                sizeof(int));

            // 移除边框颜色
            int borderColor = Win32WindowApi.DWMWA_COLOR_NONE;
            _ = Win32WindowApi.DwmSetWindowAttribute(
                _hwnd,
                Win32WindowApi.DWMWA_BORDER_COLOR,
                ref borderColor,
                sizeof(int));

            // 设置标题栏颜色为完全透明（ARGB: 0x01000000 - 几乎透明的黑色，让 Acrylic 透过）
            int captionColor = 0x01000000;
            _ = Win32WindowApi.DwmSetWindowAttribute(
                _hwnd,
                Win32WindowApi.DWMWA_CAPTION_COLOR,
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

            _ = Win32WindowApi.SetWindowLongPtr(_hwnd, Win32WindowApi.GWL_STYLE, _baseWindowStyle);
            if (_hasCapturedBaseExtendedWindowStyle)
            {
                _ = Win32WindowApi.SetWindowLongPtr(_hwnd, Win32WindowApi.GWL_EXSTYLE, _baseExtendedWindowStyle);
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

            _ = Win32WindowApi.SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                Win32WindowApi.SWP_NOSIZE |
                Win32WindowApi.SWP_NOMOVE |
                Win32WindowApi.SWP_NOZORDER |
                Win32WindowApi.SWP_NOACTIVATE |
                Win32WindowApi.SWP_FRAMECHANGED);
        }

        private void ApplyPinnedWindowFrame(Win32WindowApi.RECT approvedRect)
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyWindowRect(approvedRect);

            if (TryGetExtendedFrameBounds(out Win32WindowApi.RECT actualBounds))
            {
                Win32WindowApi.RECT correctedRect = new()
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

        private void ApplyWindowRect(Win32WindowApi.RECT rect)
        {
            int width = Math.Max(_state.MinWindowWidth, rect.Right - rect.Left);
            int height = Math.Max(1, rect.Bottom - rect.Top);

            _ = Win32WindowApi.SetWindowPos(
                _hwnd,
                Win32WindowApi.HWND_TOPMOST,
                rect.Left,
                rect.Top,
                width,
                height,
                Win32WindowApi.SWP_SHOWWINDOW);
        }

        private bool TryGetExtendedFrameBounds(out Win32WindowApi.RECT bounds)
        {
            EnsureWindowHandle();
            if (_hwnd == IntPtr.Zero)
            {
                bounds = default;
                return false;
            }

            int hr = Win32WindowApi.DwmGetWindowAttribute(
                _hwnd,
                Win32WindowApi.DWMWA_EXTENDED_FRAME_BOUNDS,
                out bounds,
                System.Runtime.InteropServices.Marshal.SizeOf<Win32WindowApi.RECT>());

            return hr >= 0;
        }

        private void RegisterAppBarIfNeeded()
        {
            if (_isAppBarRegistered)
            {
                return;
            }

            Win32WindowApi.APPBARDATA appBarData = CreateAppBarData();
            _ = Win32WindowApi.SHAppBarMessage(Win32WindowApi.ABM_NEW, ref appBarData);
            _isAppBarRegistered = true;
        }

        private void RemoveAppBar()
        {
            if (!_isAppBarRegistered)
            {
                return;
            }

            Win32WindowApi.APPBARDATA appBarData = CreateAppBarData();
            _ = Win32WindowApi.SHAppBarMessage(Win32WindowApi.ABM_REMOVE, ref appBarData);
            _isAppBarRegistered = false;
        }

        private Win32WindowApi.APPBARDATA CreateAppBarData()
        {
            EnsureWindowHandle();
            return new Win32WindowApi.APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32WindowApi.APPBARDATA>(),
                hWnd = _hwnd,
                uCallbackMessage = _appBarMessageId
            };
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            RemoveAppBar();
        }

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (_viewModel.IsDockPinned)
            {
                if (msg == Win32WindowApi.WM_NCCALCSIZE)
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

                if (msg == Win32WindowApi.WM_NCPAINT)
                {
                    System.Diagnostics.Debug.WriteLine("WM_NCPAINT: Blocking non-client paint");
                    return IntPtr.Zero;
                }

                if (msg == Win32WindowApi.WM_NCACTIVATE)
                {
                    System.Diagnostics.Debug.WriteLine("WM_NCACTIVATE: Returning 1 without drawing");
                    return new IntPtr(1);
                }
            }

            return Win32WindowApi.CallWindowProc(_originalWindowProc, hWnd, msg, wParam, lParam);
        }
    }
}

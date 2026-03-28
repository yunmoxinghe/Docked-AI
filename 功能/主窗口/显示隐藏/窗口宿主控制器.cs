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
        private IntPtr _hwnd;
        private readonly uint _appBarMessageId;

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

            InitializeWindow();
        }

        public void ToggleWindow()
        {
            EnsureWindowHandle();

            if (_viewModel.IsWindowVisible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void InitializeWindow()
        {
            _titleBarService.ConfigureTitleBarAndBorder(_window);
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

            _titleBarService.ConfigureTitleBarAndBorder(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);
            StartInitialSlideIn();
        }

        private void OnActivationChanged(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated &&
                _viewModel.IsWindowVisible &&
                !_viewModel.IsDockPinned)
            {
                HideWindow();
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
            _titleBarService.ConfigureTitleBarAndBorder(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);

            if (_viewModel.IsDockPinned)
            {
                ShowPinnedDock();
                return;
            }

            MoveWindowToStandardDock(prepareForShow: true);

            _window.Activate();
            _animationController.StartShow();
        }

        private void HideWindow()
        {
            _viewModel.MarkHidden();
            RemoveAppBar();
            SetTopMost(false);

            if (_window.AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped)
            {
                var presenter = _window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                    _layoutService.Refresh(_state);
                    Win32WindowApi.SetWindowPos(_hwnd, IntPtr.Zero, _state.TargetX, (int)_state.TargetY, _state.WindowWidth, _state.WindowHeight, 0);
                    _state.CurrentX = _state.TargetX;
                    _state.CurrentY = _state.TargetY;
                }
            }

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
            _titleBarService.ConfigureTitleBarAndBorder(_window);
            _backdropService.EnsureAcrylicBackdrop(_window);

            ApplyPinnedBounds();
            _window.Activate();
        }

        private void RestoreStandardDock()
        {
            RemoveAppBar();
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
            _state.CurrentX = _state.TargetX;
            _state.CurrentY = _state.TargetY;
            RegisterAppBarIfNeeded();

            Win32WindowApi.APPBARDATA appBarData = CreateAppBarData();
            int desiredWidth = _state.WindowWidth;

            appBarData.uEdge = Win32WindowApi.ABE_RIGHT;
            appBarData.rc.Top = 0;
            appBarData.rc.Bottom = _state.ScreenHeight;
            appBarData.rc.Right = _state.ScreenWidth;
            appBarData.rc.Left = appBarData.rc.Right - desiredWidth;

            _ = Win32WindowApi.SHAppBarMessage(Win32WindowApi.ABM_QUERYPOS, ref appBarData);

            appBarData.rc.Right = _state.ScreenWidth;
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
                _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(_state.TargetX, (int)_state.TargetY, width, height));
                SetTopMost(true);
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
            }
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
    }
}

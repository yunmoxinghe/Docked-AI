using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.MainWindow.Appearance;
using Docked_AI.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
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
        private IntPtr _hwnd;

        public WindowHostController(Window window, MainWindowViewModel viewModel)
        {
            _window = window;
            _viewModel = viewModel;
            _layoutService = new WindowLayoutService();
            _state = _layoutService.CreateInitialState();
            _titleBarService = new TitleBarService();
            _backdropService = new BackdropService();
            _animationController = new SlideAnimationController(_window, _state);

            InitializeWindow();
        }

        public void ToggleWindow()
        {
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            }

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
            if (args.WindowActivationState == WindowActivationState.Deactivated && _viewModel.IsWindowVisible)
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

            _layoutService.PrepareForShow(_state);
            _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32((int)_state.CurrentX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));

            _window.Activate();
            _animationController.StartShow();
        }

        private void HideWindow()
        {
            _viewModel.MarkHidden();

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
    }
}

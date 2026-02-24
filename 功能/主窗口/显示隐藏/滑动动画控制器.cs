using Docked_AI.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Visibility
{
    internal sealed class SlideAnimationController
    {
        private readonly Window _window;
        private readonly WindowLayoutState _state;
        private readonly IntPtr _hwnd;
        private DateTime _animationStartTime;
        private double _startX;
        private bool _isVisible;

        private readonly TimeSpan _showAnimationDuration = TimeSpan.FromMilliseconds(220);
        private readonly TimeSpan _hideAnimationDuration = TimeSpan.FromMilliseconds(180);

        public SlideAnimationController(Window window, WindowLayoutState state)
        {
            _window = window;
            _state = state;
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        }

        public void StartShow()
        {
            _isVisible = true;
            _animationStartTime = DateTime.Now;
            _startX = _state.CurrentX;
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        public void StartHide()
        {
            _isVisible = false;
            _animationStartTime = DateTime.Now;
            _startX = _state.CurrentX;
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        private void OnFrame(object? sender, object e)
        {
            var elapsed = DateTime.Now - _animationStartTime;
            double progress;
            double easedProgress;

            if (_isVisible)
            {
                progress = Math.Min(elapsed.TotalMilliseconds / _showAnimationDuration.TotalMilliseconds, 1.0);
                easedProgress = 1 - Math.Pow(1 - progress, 3);
            }
            else
            {
                progress = Math.Min(elapsed.TotalMilliseconds / _hideAnimationDuration.TotalMilliseconds, 1.0);
                easedProgress = 1 - Math.Pow(1 - progress, 2);
            }

            _state.CurrentX = _startX + (_state.TargetX - _startX) * easedProgress;
            int newX = (int)Math.Round(_state.CurrentX);

            if (progress >= 1.0)
            {
                newX = _state.TargetX;
                _state.CurrentX = _state.TargetX;
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnFrame;

                if (!_isVisible)
                {
                    Win32WindowApi.ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window), Win32WindowApi.SW_HIDE);
                }
            }

            if (_hwnd != IntPtr.Zero)
            {
                _ = Win32WindowApi.SetWindowPos(
                    _hwnd,
                    IntPtr.Zero,
                    newX,
                    (int)_state.CurrentY,
                    0,
                    0,
                    Win32WindowApi.SWP_NOSIZE | Win32WindowApi.SWP_NOZORDER | Win32WindowApi.SWP_NOACTIVATE | Win32WindowApi.SWP_NOOWNERZORDER);
            }
            else
            {
                _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
            }
        }
    }
}

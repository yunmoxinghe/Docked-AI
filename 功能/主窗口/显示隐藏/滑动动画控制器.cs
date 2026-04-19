using Docked_AI.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Visibility
{
    /// <summary>
    /// 滑动动画控制器 - 执行窗口滑入/滑出动画
    /// 
    /// 【文件职责】
    /// 1. 执行窗口的滑动动画（显示时从右侧滑入，隐藏时滑出到右侧）
    /// 2. 使用帧渲染事件实现平滑动画
    /// 3. 应用缓动函数（Ease-out）提升动画体验
    /// 
    /// 【核心设计】
    /// 
    /// 为什么使用 CompositionTarget.Rendering 事件？
    /// - 与屏幕刷新率同步（通常 60fps），动画更流畅
    /// - 自动处理帧率波动，避免动画卡顿
    /// - 比 DispatcherTimer 更精确，延迟更低
    /// 
    /// 为什么使用缓动函数？
    /// - 显示动画：Ease-out cubic (1 - (1-t)³)，快速启动，缓慢停止
    /// - 隐藏动画：Ease-out quadratic (1 - (1-t)²)，更快的动画速度
    /// - 提升用户体验，避免线性动画的生硬感
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 显示动画流程：
    ///   1. StartShow() 初始化动画参数（起始位置、目标位置、开始时间）
    ///   2. 订阅 CompositionTarget.Rendering 事件
    ///   3. OnFrame() 每帧计算当前位置（基于缓动函数）
    ///   4. 调用 SetWindowPos 更新窗口位置
    ///   5. 动画完成后取消订阅事件
    /// 
    /// 隐藏动画流程：
    ///   1. StartHide() 初始化动画参数
    ///   2. 订阅 CompositionTarget.Rendering 事件
    ///   3. OnFrame() 每帧计算当前位置
    ///   4. 调用 SetWindowPos 更新窗口位置
    ///   5. 动画完成后调用 ShowWindow(SW_HIDE) 隐藏窗口
    ///   6. 取消订阅事件
    /// 
    /// 【关键依赖关系】
    /// - Window: WinUI 窗口对象，提供 AppWindow API
    /// - WindowLayoutState: 布局状态，提供起始位置、目标位置、窗口尺寸
    /// - VisibilityWin32Api: Win32 API 封装，提供 SetWindowPos、ShowWindow
    /// 
    /// 【潜在副作用】
    /// 1. 每帧调用 SetWindowPos，频繁更新窗口位置
    /// 2. 订阅 CompositionTarget.Rendering 事件（必须在动画完成后取消订阅）
    /// 3. 隐藏动画完成后调用 ShowWindow(SW_HIDE)，窗口不可见
    /// 
    /// 【重构风险点】
    /// 1. 动画持续时间：
    ///    - 显示动画 220ms，隐藏动画 180ms
    ///    - 如果修改持续时间，需要同步修改 Controller 中的延迟时间
    /// 2. 缓动函数：
    ///    - 显示动画使用 cubic，隐藏动画使用 quadratic
    ///    - 如果修改缓动函数，需要测试动画效果
    /// 3. 事件订阅：
    ///    - 必须在动画完成后取消订阅 CompositionTarget.Rendering
    ///    - 否则导致内存泄漏和性能问题
    /// 4. 窗口句柄：
    ///    - 如果窗口句柄为 IntPtr.Zero，回退到 AppWindow.MoveAndResize
    ///    - 但 AppWindow API 不支持动画，会导致闪烁
    /// 5. 隐藏动画的 ShowWindow 调用：
    ///    - 必须在动画完成后调用，否则窗口会提前隐藏
    ///    - 如果忘记调用，窗口会停留在屏幕外，但仍然可见
    /// </summary>
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

        /// <summary>
        /// 构造函数 - 初始化动画控制器
        /// 
        /// 【参数说明】
        /// - window: WinUI 窗口对象
        /// - state: 布局状态，提供起始位置和目标位置
        /// 
        /// 【设计原因】
        /// 为什么在构造函数中获取窗口句柄？
        /// - 窗口句柄在窗口创建后不会改变
        /// - 提前获取避免每帧都调用 GetWindowHandle
        /// </summary>
        public SlideAnimationController(Window window, WindowLayoutState state)
        {
            _window = window;
            _state = state;
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        }

        /// <summary>
        /// 开始显示动画 - 窗口从右侧滑入
        /// 
        /// 【调用时机】
        /// WindowHostController.StartShowAnimation() 调用
        /// 
        /// 【动画参数】
        /// - 起始位置：_state.CurrentX（通常是屏幕外）
        /// - 目标位置：_state.TargetX（屏幕右侧边缘）
        /// - 持续时间：220ms
        /// - 缓动函数：Ease-out cubic
        /// </summary>
        public void StartShow()
        {
            _isVisible = true;
            _animationStartTime = DateTime.Now;
            _startX = _state.CurrentX;
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        /// <summary>
        /// 开始隐藏动画 - 窗口滑出到右侧
        /// 
        /// 【调用时机】
        /// WindowHostController.StartHideAnimation() 调用
        /// 
        /// 【动画参数】
        /// - 起始位置：_state.CurrentX（当前位置）
        /// - 目标位置：_state.TargetX（屏幕外）
        /// - 持续时间：180ms
        /// - 缓动函数：Ease-out quadratic
        /// 
        /// 【副作用】
        /// 动画完成后调用 ShowWindow(SW_HIDE) 隐藏窗口
        /// </summary>
        public void StartHide()
        {
            _isVisible = false;
            _animationStartTime = DateTime.Now;
            _startX = _state.CurrentX;
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        /// <summary>
        /// 帧渲染事件处理器 - 每帧更新窗口位置
        /// 
        /// 【核心逻辑】
        /// 1. 计算动画进度（0.0 到 1.0）
        /// 2. 应用缓动函数（显示用 cubic，隐藏用 quadratic）
        /// 3. 计算当前位置（线性插值）
        /// 4. 调用 SetWindowPos 更新窗口位置
        /// 5. 动画完成后取消订阅事件
        /// 
        /// 【缓动函数】
        /// - 显示动画：easedProgress = 1 - (1 - progress)³
        ///   快速启动，缓慢停止，给人流畅的感觉
        /// - 隐藏动画：easedProgress = 1 - (1 - progress)²
        ///   更快的动画速度，快速隐藏窗口
        /// 
        /// 【性能优化】
        /// - 使用 SetWindowPos 而不是 AppWindow.MoveAndResize
        /// - SetWindowPos 更快，支持 SWP_NOACTIVATE 标志
        /// - 如果窗口句柄为 IntPtr.Zero，回退到 AppWindow API
        /// </summary>
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
                    VisibilityWin32Api.ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window), VisibilityWin32Api.SW_HIDE);
                }
            }

            if (_hwnd != IntPtr.Zero)
            {
                _ = VisibilityWin32Api.SetWindowPos(
                    _hwnd,
                    IntPtr.Zero,
                    newX,
                    (int)_state.CurrentY,
                    0,
                    0,
                    VisibilityWin32Api.SWP_NOSIZE | VisibilityWin32Api.SWP_NOZORDER | VisibilityWin32Api.SWP_NOACTIVATE | VisibilityWin32Api.SWP_NOOWNERZORDER);
            }
            else
            {
                _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
            }
        }
    }
}

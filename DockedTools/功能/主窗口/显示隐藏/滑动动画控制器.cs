using DockedTools.Features.MainWindow.Placement;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;

namespace DockedTools.Features.MainWindow.Visibility
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
    /// 【性能优化】
    /// - 使用 Stopwatch 替代 DateTime.Now（精度 < 1ms vs 15ms）
    /// - 添加 SWP_ASYNCWINDOWPOS 标志减少 UI 线程阻塞
    /// - 预计算缓动曲线查找表减少每帧计算开销
    /// - 性能监控统计帧数和平均帧时间
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
        private readonly Stopwatch _animationTimer = new();
        private double _startX;
        private bool _isVisible;

        // 性能监控
        private int _frameCount;
        private long _totalFrameTicks;

        private readonly TimeSpan _showAnimationDuration = TimeSpan.FromMilliseconds(220);
        private readonly TimeSpan _hideAnimationDuration = TimeSpan.FromMilliseconds(180);

        // 预计算缓动曲线查找表（60fps * 220ms = 13帧显示，60fps * 180ms = 11帧隐藏）
        private static readonly float[] _easeOutCubicLUT;
        private static readonly float[] _easeOutQuadraticLUT;

        static SlideAnimationController()
        {
            // 显示动画：Ease-out cubic，预计算 60 个点
            _easeOutCubicLUT = new float[61];
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                _easeOutCubicLUT[i] = 1f - (float)Math.Pow(1 - t, 3);
            }

            // 隐藏动画：Ease-out quadratic，预计算 60 个点
            _easeOutQuadraticLUT = new float[61];
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                _easeOutQuadraticLUT[i] = 1f - (float)Math.Pow(1 - t, 2);
            }
        }

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
            _startX = _state.CurrentX;
            _frameCount = 0;
            _totalFrameTicks = 0;
            _animationTimer.Restart();
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
            _startX = _state.CurrentX;
            _frameCount = 0;
            _totalFrameTicks = 0;
            _animationTimer.Restart();
            Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnFrame;
        }

        /// <summary>
        /// 帧渲染事件处理器 - 每帧更新窗口位置
        /// 
        /// 【核心逻辑】
        /// 1. 计算动画进度（0.0 到 1.0）
        /// 2. 从预计算的查找表获取缓动值（无需每帧计算）
        /// 3. 计算当前位置（线性插值）
        /// 4. 调用 SetWindowPos 更新窗口位置（添加 ASYNCWINDOWPOS 标志）
        /// 5. 动画完成后取消订阅事件并输出性能统计
        /// 
        /// 【缓动函数】
        /// - 显示动画：使用预计算的 Ease-out cubic 查找表
        ///   快速启动，缓慢停止，给人流畅的感觉
        /// - 隐藏动画：使用预计算的 Ease-out quadratic 查找表
        ///   更快的动画速度，快速隐藏窗口
        /// 
        /// 【性能优化】
        /// - 使用 Stopwatch 高精度计时（< 1ms vs DateTime.Now 的 15ms）
        /// - 使用查找表避免每帧计算 Math.Pow
        /// - SetWindowPos 添加 SWP_ASYNCWINDOWPOS 减少阻塞
        /// - 统计帧性能并输出到调试日志
        /// </summary>
        private void OnFrame(object? sender, object e)
        {
            long frameStartTicks = Stopwatch.GetTimestamp();

            var elapsed = _animationTimer.Elapsed;
            double progress;
            double easedProgress;

            if (_isVisible)
            {
                progress = Math.Min(elapsed.TotalMilliseconds / _showAnimationDuration.TotalMilliseconds, 1.0);
                // 从查找表获取缓动值
                int lutIndex = Math.Min((int)(progress * 60), 60);
                easedProgress = _easeOutCubicLUT[lutIndex];
            }
            else
            {
                progress = Math.Min(elapsed.TotalMilliseconds / _hideAnimationDuration.TotalMilliseconds, 1.0);
                // 从查找表获取缓动值
                int lutIndex = Math.Min((int)(progress * 60), 60);
                easedProgress = _easeOutQuadraticLUT[lutIndex];
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

                // 输出性能统计
                if (_frameCount > 0)
                {
                    double avgFrameTimeMs = (_totalFrameTicks * 1000.0 / Stopwatch.Frequency) / _frameCount;
                    double actualFps = _frameCount * 1000.0 / _animationTimer.Elapsed.TotalMilliseconds;
                    System.Diagnostics.Debug.WriteLine($"[SlideAnimation] {(_isVisible ? "Show" : "Hide")} completed: {_frameCount} frames, {actualFps:F1} fps, avg {avgFrameTimeMs:F2}ms/frame");
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
                    VisibilityWin32Api.SWP_NOSIZE | VisibilityWin32Api.SWP_NOZORDER | 
                    VisibilityWin32Api.SWP_NOACTIVATE | VisibilityWin32Api.SWP_NOOWNERZORDER | 
                    VisibilityWin32Api.SWP_ASYNCWINDOWPOS);  // 添加异步标志减少阻塞
            }
            else
            {
                _window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, (int)_state.CurrentY, _state.WindowWidth, _state.WindowHeight));
            }

            // 统计帧性能
            long frameEndTicks = Stopwatch.GetTimestamp();
            _frameCount++;
            _totalFrameTicks += (frameEndTicks - frameStartTicks);
        }
    }
}

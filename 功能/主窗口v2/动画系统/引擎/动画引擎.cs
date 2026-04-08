using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Docked_AI.功能.主窗口v2.动画系统.视觉状态;
using Docked_AI.功能.主窗口v2.基础设施.日志;

namespace Docked_AI.功能.主窗口v2.动画系统.引擎;

/// <summary>
/// 统一动画引擎，负责执行所有视觉状态插值
/// 支持线性插值（LERP）和 Spring 物理模拟
/// 使用时间驱动而非帧驱动，确保动画时长准确
/// </summary>
public class AnimationEngine
{
    /// <summary>
    /// 动画性能日志记录器（可选）
    /// </summary>
    private readonly AnimationPerformanceLogger? _performanceLogger;
    
    /// <summary>
    /// 创建动画引擎
    /// </summary>
    /// <param name="logger">日志记录器（可选）</param>
    public AnimationEngine(ILogger? logger = null)
    {
        if (logger != null)
        {
            _performanceLogger = new AnimationPerformanceLogger(logger);
        }
    }
    /// <summary>
    /// 执行动画，从 from 插值到 to
    /// </summary>
    /// <param name="from">起始视觉状态</param>
    /// <param name="to">目标视觉状态</param>
    /// <param name="spec">动画规格</param>
    /// <param name="onProgress">进度回调，实时更新视觉状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">当必需参数为 null 时抛出</exception>
    /// <exception cref="OperationCanceledException">当动画被取消时抛出</exception>
    public async Task Animate(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken)
    {
        // 参数验证
        if (from == null) throw new ArgumentNullException(nameof(from));
        if (to == null) throw new ArgumentNullException(nameof(to));
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (onProgress == null) throw new ArgumentNullException(nameof(onProgress));

        // 根据动画规格选择插值策略
        if (spec.Spring != null)
        {
            await AnimateWithSpring(from, to, spec, onProgress, cancellationToken);
        }
        else
        {
            await AnimateWithEasing(from, to, spec, onProgress, cancellationToken);
        }
    }

    /// <summary>
    /// 使用缓动函数执行动画
    /// 基于真实时间（而非帧数）计算进度，确保动画时长准确
    /// </summary>
    /// <param name="from">起始视觉状态</param>
    /// <param name="to">目标视觉状态</param>
    /// <param name="spec">动画规格</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task AnimateWithEasing(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken)
    {
        // 记录动画开始
        var animationType = $"Easing({spec.Easing})";
        _performanceLogger?.LogAnimationStarted(animationType, spec.Duration);
        
        // 使用 Stopwatch 实现时间驱动（而非帧驱动）
        var stopwatch = Stopwatch.StartNew();
        var duration = spec.Duration;
        var frameCount = 0;

        try
        {
            while (true)
            {
                // 🔴 关键修复：在循环开始时立即检查取消令牌
                cancellationToken.ThrowIfCancellationRequested();

                // 根据真实时间计算进度（而非帧数）
                var elapsed = stopwatch.Elapsed;
                var progress = Math.Min(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);
                
                // 应用缓动函数
                var easedProgress = ApplyEasing(progress, spec.Easing);

                // 插值所有属性
                var current = Lerp(from, to, easedProgress);
                
                // 🔴 关键修复：在调用回调前再次检查取消令牌
                // 这确保即使在计算过程中取消，也能立即响应
                cancellationToken.ThrowIfCancellationRequested();
                
                // 🔴 关键修复：保护回调调用，确保回调中的异常不会被吞掉
                // 但允许 OperationCanceledException 正常传播
                try
                {
                    onProgress(current);
                    frameCount++;
                }
                catch (OperationCanceledException)
                {
                    // 允许取消异常传播
                    throw;
                }
                catch (Exception ex)
                {
                    // 其他异常包装后重新抛出，保留堆栈跟踪
                    throw new InvalidOperationException($"Animation progress callback failed: {ex.Message}", ex);
                }

                // 动画完成
                if (progress >= 1.0) break;

                // 等待下一帧（约 60 FPS）
                // 重要：必须传递 cancellationToken 以便动画能被立即打断
                try
                {
                    await Task.Delay(16, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Task.Delay 被取消时会抛出 TaskCanceledException
                    // 将其转换为 OperationCanceledException 以保持一致性
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            
            // 动画成功完成
            stopwatch.Stop();
            _performanceLogger?.LogAnimationCompleted(animationType, spec.Duration, stopwatch.Elapsed, frameCount);
        }
        catch (OperationCanceledException)
        {
            // 动画被中断
            stopwatch.Stop();
            _performanceLogger?.LogAnimationInterrupted(animationType, stopwatch.Elapsed, frameCount);
            throw;
        }
    }

    /// <summary>
    /// 使用 Spring 物理模拟执行动画
    /// 基于物理模拟计算位移和速度，提供自然的弹性效果
    /// </summary>
    /// <param name="from">起始视觉状态</param>
    /// <param name="to">目标视觉状态</param>
    /// <param name="spec">动画规格</param>
    /// <param name="onProgress">进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task AnimateWithSpring(
        WindowVisualState from,
        WindowVisualState to,
        AnimationSpec spec,
        Action<WindowVisualState> onProgress,
        CancellationToken cancellationToken)
    {
        // 记录动画开始
        var animationType = $"Spring(k={spec.Spring!.Stiffness}, c={spec.Spring.Damping})";
        _performanceLogger?.LogAnimationStarted(animationType, spec.Duration);
        
        // Spring 物理模拟实现
        var stopwatch = Stopwatch.StartNew();
        var velocity = 0.0;  // 初始速度
        var displacement = 1.0;  // 初始位移（归一化，1.0 表示完全在起点）
        var frameCount = 0;

        // Spring 稳定阈值：当速度和位移都足够小时，认为动画完成
        const double velocityThreshold = 0.5;  // 速度阈值（像素/秒）
        const double displacementThreshold = 0.001;  // 位移阈值（归一化）

        try
        {
            while (true)
            {
                // 🔴 关键修复：在循环开始时立即检查取消令牌
                cancellationToken.ThrowIfCancellationRequested();

                var deltaTime = 0.016;  // ~60 FPS

                // Spring 物理计算
                // F_spring = -k * x (胡克定律)
                // F_damping = -c * v (阻尼力)
                // a = F / m (假设 m = 1)
                var springForce = -spec.Spring!.Stiffness * displacement;
                var dampingForce = -spec.Spring.Damping * velocity;
                var acceleration = springForce + dampingForce;

                // 更新速度和位移
                velocity += acceleration * deltaTime;
                displacement += velocity * deltaTime;

                // 计算当前进度（1.0 - displacement，因为 displacement 从 1.0 趋向 0.0）
                var progress = Math.Max(0.0, Math.Min(1.0, 1.0 - displacement));

                // 插值所有属性
                var current = Lerp(from, to, progress);
                
                // 🔴 关键修复：在调用回调前再次检查取消令牌
                // 这确保即使在计算过程中取消，也能立即响应
                cancellationToken.ThrowIfCancellationRequested();
                
                // 🔴 关键修复：保护回调调用，确保回调中的异常不会被吞掉
                // 但允许 OperationCanceledException 正常传播
                try
                {
                    onProgress(current);
                    frameCount++;
                }
                catch (OperationCanceledException)
                {
                    // 允许取消异常传播
                    throw;
                }
                catch (Exception ex)
                {
                    // 其他异常包装后重新抛出，保留堆栈跟踪
                    throw new InvalidOperationException($"Animation progress callback failed: {ex.Message}", ex);
                }

                // 检查稳定条件：速度和位移都足够小
                if (Math.Abs(velocity) < velocityThreshold && Math.Abs(displacement) < displacementThreshold)
                {
                    // 🔴 关键修复：在最终回调前检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 确保最终状态精确到达目标
                    try
                    {
                        onProgress(to);
                        frameCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Animation progress callback failed: {ex.Message}", ex);
                    }
                    break;
                }

                // 等待下一帧（约 60 FPS）
                // 重要：必须传递 cancellationToken 以便动画能被立即打断
                try
                {
                    await Task.Delay(16, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Task.Delay 被取消时会抛出 TaskCanceledException
                    // 将其转换为 OperationCanceledException 以保持一致性
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            
            // 动画成功完成
            stopwatch.Stop();
            _performanceLogger?.LogAnimationCompleted(animationType, spec.Duration, stopwatch.Elapsed, frameCount);
        }
        catch (OperationCanceledException)
        {
            // 动画被中断
            stopwatch.Stop();
            _performanceLogger?.LogAnimationInterrupted(animationType, stopwatch.Elapsed, frameCount);
            throw;
        }
    }

    /// <summary>
    /// 线性插值两个 WindowVisualState
    /// 对所有连续量属性应用相同的插值进度
    /// </summary>
    /// <param name="from">起始状态</param>
    /// <param name="to">目标状态</param>
    /// <param name="t">插值进度（0.0 - 1.0）</param>
    /// <returns>插值后的视觉状态</returns>
    private WindowVisualState Lerp(WindowVisualState from, WindowVisualState to, double t)
    {
        return new WindowVisualState
        {
            // 插值位置和尺寸
            Bounds = new Rect(
                Lerp(from.Bounds.X, to.Bounds.X, t),
                Lerp(from.Bounds.Y, to.Bounds.Y, t),
                Lerp(from.Bounds.Width, to.Bounds.Width, t),
                Lerp(from.Bounds.Height, to.Bounds.Height, t)
            ),
            // 插值圆角半径
            CornerRadius = Lerp(from.CornerRadius, to.CornerRadius, t),
            // 插值不透明度
            Opacity = Lerp(from.Opacity, to.Opacity, t),
            // 离散属性：直接使用目标值（不插值）
            IsTopmost = to.IsTopmost,
            ExtendedStyle = to.ExtendedStyle
        };
    }

    /// <summary>
    /// 线性插值两个 double 值
    /// </summary>
    /// <param name="a">起始值</param>
    /// <param name="b">目标值</param>
    /// <param name="t">插值进度（0.0 - 1.0）</param>
    /// <returns>插值后的值</returns>
    private double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// 应用缓动函数到线性进度
    /// </summary>
    /// <param name="t">线性进度（0.0 - 1.0）</param>
    /// <param name="easing">缓动函数类型</param>
    /// <returns>缓动后的进度（0.0 - 1.0）</returns>
    private double ApplyEasing(double t, Easing easing)
    {
        return easing switch
        {
            Easing.Linear => t,
            Easing.EaseIn => t * t,
            Easing.EaseOut => t * (2 - t),
            Easing.EaseInOut => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t,
            Easing.EaseInCubic => t * t * t,
            Easing.EaseOutCubic => (--t) * t * t + 1,
            Easing.EaseInOutCubic => t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1,
            _ => t
        };
    }
}

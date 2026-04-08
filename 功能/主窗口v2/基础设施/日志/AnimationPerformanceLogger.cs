using System;
using System.Diagnostics;

namespace Docked_AI.功能.主窗口v2.基础设施.日志;

/// <summary>
/// 动画性能日志记录器
/// 用于记录动画执行的性能指标
/// </summary>
public class AnimationPerformanceLogger
{
    private readonly ILogger _logger;
    
    public AnimationPerformanceLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// 记录动画开始
    /// </summary>
    public void LogAnimationStarted(string animationType, TimeSpan duration)
    {
        _logger.Debug($"Animation started: {animationType}, duration: {duration.TotalMilliseconds:F0}ms");
    }
    
    /// <summary>
    /// 记录动画完成
    /// </summary>
    public void LogAnimationCompleted(string animationType, TimeSpan plannedDuration, TimeSpan actualDuration, int frameCount)
    {
        var fps = frameCount / actualDuration.TotalSeconds;
        _logger.Performance($"Animation completed: {animationType}", actualDuration.TotalMilliseconds, "ms");
        _logger.Performance($"Animation FPS: {animationType}", fps, "fps");
        _logger.Performance($"Animation frame count: {animationType}", frameCount, "frames");
        
        // 记录时长偏差
        var deviation = Math.Abs(actualDuration.TotalMilliseconds - plannedDuration.TotalMilliseconds);
        if (deviation > 50) // 偏差超过 50ms 时记录警告
        {
            _logger.Warning($"Animation duration deviation: planned {plannedDuration.TotalMilliseconds:F0}ms, actual {actualDuration.TotalMilliseconds:F0}ms (deviation: {deviation:F0}ms)");
        }
    }
    
    /// <summary>
    /// 记录动画被中断
    /// </summary>
    public void LogAnimationInterrupted(string animationType, TimeSpan elapsedTime, int frameCount)
    {
        var fps = frameCount / elapsedTime.TotalSeconds;
        _logger.Info($"Animation interrupted: {animationType}, elapsed: {elapsedTime.TotalMilliseconds:F0}ms, frames: {frameCount}, fps: {fps:F1}");
    }
    
    /// <summary>
    /// 记录帧率下降
    /// </summary>
    public void LogFrameRateDrop(double currentFps, double targetFps)
    {
        _logger.Warning($"Frame rate drop detected: current {currentFps:F1} fps, target {targetFps:F1} fps");
    }
}

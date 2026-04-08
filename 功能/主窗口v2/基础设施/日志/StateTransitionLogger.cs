using System;
using Docked_AI.功能.主窗口v2.状态机;

namespace Docked_AI.功能.主窗口v2.基础设施.日志;

/// <summary>
/// 状态转换日志记录器
/// 专门用于记录窗口状态转换的完整生命周期
/// </summary>
public class StateTransitionLogger
{
    private readonly ILogger _logger;
    
    public StateTransitionLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// 记录状态转换开始
    /// </summary>
    public void LogTransitionStarted(WindowState from, WindowState to)
    {
        _logger.Info($"Transition started: {from} → {to}");
    }
    
    /// <summary>
    /// 记录状态转换完成
    /// </summary>
    public void LogStateChanged(WindowState from, WindowState to)
    {
        _logger.Info($"State changed: {from} → {to}");
    }
    
    /// <summary>
    /// 记录状态转换失败
    /// </summary>
    public void LogTransitionFailed(WindowState from, WindowState to, Exception exception)
    {
        _logger.Error($"Transition failed: {from} → {to}", exception);
    }
    
    /// <summary>
    /// 记录状态转换被取消
    /// </summary>
    public void LogTransitionCancelled(WindowState from, WindowState to)
    {
        _logger.Info($"Transition cancelled: {from} → {to}");
    }
    
    /// <summary>
    /// 记录关键故障
    /// </summary>
    public void LogCriticalFailure(int consecutiveFailures)
    {
        _logger.Error($"Critical failure: {consecutiveFailures} consecutive failures. Automatic transitions disabled.");
    }
    
    /// <summary>
    /// 记录重试尝试
    /// </summary>
    public void LogRetryAttempt(WindowState from, WindowState to, int attemptNumber, int maxRetries)
    {
        _logger.Warning($"Retrying transition {from} → {to} (attempt {attemptNumber}/{maxRetries})");
    }
    
    /// <summary>
    /// 记录恢复到安全状态
    /// </summary>
    public void LogRevertToSafeState(WindowState safeState)
    {
        _logger.Warning($"Reverting to safe state: {safeState}");
    }
}

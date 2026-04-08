using System;

namespace Docked_AI.功能.主窗口v2.基础设施.日志;

/// <summary>
/// 资源管理日志记录器
/// 用于记录 CancellationTokenSource 等资源的创建和释放
/// </summary>
public class ResourceManagementLogger
{
    private readonly ILogger _logger;
    
    public ResourceManagementLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// 记录 CancellationTokenSource 创建
    /// </summary>
    public void LogCtsCreated(string context)
    {
        _logger.Debug($"CancellationTokenSource created: {context}");
    }
    
    /// <summary>
    /// 记录 CancellationTokenSource 取消
    /// </summary>
    public void LogCtsCancelled(string context)
    {
        _logger.Debug($"CancellationTokenSource cancelled: {context}");
    }
    
    /// <summary>
    /// 记录 CancellationTokenSource 释放
    /// </summary>
    public void LogCtsDisposed(string context)
    {
        _logger.Debug($"CancellationTokenSource disposed: {context}");
    }
    
    /// <summary>
    /// 记录 CancellationTokenSource 未正确释放的警告
    /// </summary>
    public void LogCtsNotDisposed(string context)
    {
        _logger.Warning($"CancellationTokenSource not properly disposed: {context}");
    }
    
    /// <summary>
    /// 记录 CancellationTokenSource 替换
    /// </summary>
    public void LogCtsReplaced(string context)
    {
        _logger.Debug($"CancellationTokenSource replaced: {context}");
    }
    
    /// <summary>
    /// 记录资源泄漏警告
    /// </summary>
    public void LogResourceLeak(string resourceType, string context)
    {
        _logger.Warning($"Potential resource leak detected: {resourceType} in {context}");
    }
}

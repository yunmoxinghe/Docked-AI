using System;

namespace Docked_AI.功能.主窗口v2.基础设施.日志;

/// <summary>
/// 日志接口，定义统一的日志记录方法
/// </summary>
public interface ILogger
{
    /// <summary>
    /// 记录调试信息
    /// </summary>
    void Debug(string message);
    
    /// <summary>
    /// 记录信息
    /// </summary>
    void Info(string message);
    
    /// <summary>
    /// 记录警告
    /// </summary>
    void Warning(string message);
    
    /// <summary>
    /// 记录错误
    /// </summary>
    void Error(string message, Exception? exception = null);
    
    /// <summary>
    /// 记录性能指标
    /// </summary>
    void Performance(string metric, double value, string? unit = null);
}

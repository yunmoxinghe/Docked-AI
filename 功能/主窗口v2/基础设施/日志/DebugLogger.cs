using System;
using System.Diagnostics;

namespace Docked_AI.功能.主窗口v2.基础设施.日志;

/// <summary>
/// 基于 System.Diagnostics.Debug 的日志实现
/// 适用于开发和调试阶段
/// </summary>
public class DebugLogger : ILogger
{
    private readonly string _category;
    
    public DebugLogger(string category)
    {
        _category = category;
    }
    
    public void Debug(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{_category}] [DEBUG] {message}");
    }
    
    public void Info(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{_category}] [INFO] {message}");
    }
    
    public void Warning(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{_category}] [WARNING] {message}");
    }
    
    public void Error(string message, Exception? exception = null)
    {
        System.Diagnostics.Debug.WriteLine($"[{_category}] [ERROR] {message}");
        if (exception != null)
        {
            System.Diagnostics.Debug.WriteLine($"[{_category}] [ERROR] Exception: {exception.GetType().Name}: {exception.Message}");
            System.Diagnostics.Debug.WriteLine($"[{_category}] [ERROR] Stack trace: {exception.StackTrace}");
        }
    }
    
    public void Performance(string metric, double value, string? unit = null)
    {
        var unitStr = unit != null ? $" {unit}" : "";
        System.Diagnostics.Debug.WriteLine($"[{_category}] [PERF] {metric}: {value:F2}{unitStr}");
    }
}

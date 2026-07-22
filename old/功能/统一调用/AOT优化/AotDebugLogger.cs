using System;
using System.IO;
using Windows.Storage;

namespace Docked_AI.Features.Shared.AotOptimization
{
    /// <summary>
    /// AOT 友好的调试日志记录器
    /// 
    /// <para>
    /// 在 Native AOT 模式下，Debug.WriteLine 可能不会输出到调试器。
    /// 此日志记录器将日志写入应用本地文件夹，方便排查问题。
    /// </para>
    /// 
    /// <para>
    /// 日志位置：%LocalAppData%\Packages\{PackageId}\LocalState\debug.log
    /// </para>
    /// </summary>
    public static class AotDebugLogger
    {
        private static readonly string LogFilePath;
        private static readonly object LockObject = new();

        static AotDebugLogger()
        {
            try
            {
                // 日志文件路径：LocalState\debug.log
                var localFolder = ApplicationData.Current.LocalFolder;
                LogFilePath = Path.Combine(localFolder.Path, "debug.log");
                
                // 启动时清空旧日志（可选）
                // File.WriteAllText(LogFilePath, $"=== Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch (Exception ex)
            {
                // 初始化失败时使用临时路径
                LogFilePath = Path.Combine(Path.GetTempPath(), "docked-ai-debug.log");
                System.Diagnostics.Debug.WriteLine($"[AotDebugLogger] Failed to initialize log file: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入调试日志（同时输出到 Debug 和文件）
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                
                // 1. 输出到 Debug（IDE 调试时可见）
                System.Diagnostics.Debug.WriteLine(logEntry);
                
                // 2. 写入文件（AOT 模式时的主要方法）
                lock (LockObject)
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // 静默失败，避免日志记录影响应用运行
            }
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogFilePath() => LogFilePath;

        /// <summary>
        /// 清空日志文件
        /// </summary>
        public static void Clear()
        {
            try
            {
                lock (LockObject)
                {
                    File.WriteAllText(LogFilePath, $"=== Log Cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                }
            }
            catch
            {
                // 静默失败
            }
        }
    }
}

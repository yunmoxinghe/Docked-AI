using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace DockedTools.Features.UnifiedCalls.Logging
{
    /// <summary>
    /// 统一日志服务
    /// 
    /// 【功能】
    /// 1. 统一的异常记录接口
    /// 2. 自动写入本地日志文件
    /// 3. 同时输出到调试控制台
    /// 4. 支持不同日志级别
    /// 
    /// 【使用方法】
    /// LogService.Error("模块名", "操作描述", exception);
    /// LogService.Warning("模块名", "警告信息");
    /// LogService.Info("模块名", "信息");
    /// LogService.Debug("模块名", "调试信息");
    /// </summary>
    public static class LogService
    {
        private static readonly object _fileLock = new object();
        private static string? _logDirectory;

        /// <summary>
        /// 日志级别
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        private static void EnsureLogDirectory()
        {
            if (_logDirectory != null)
            {
                return;
            }

            try
            {
                _logDirectory = Path.Combine(
                    Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                    "logs");
                Directory.CreateDirectory(_logDirectory);
            }
            catch
            {
                // 如果无法创建日志目录，使用临时目录
                _logDirectory = Path.Combine(Path.GetTempPath(), "DockedAI_Logs");
                try
                {
                    Directory.CreateDirectory(_logDirectory);
                }
                catch
                {
                    // 完全失败，只输出到控制台
                    _logDirectory = null;
                }
            }
        }

        /// <summary>
        /// 记录错误（带异常）
        /// </summary>
        /// <param name="module">模块名称</param>
        /// <param name="operation">操作描述</param>
        /// <param name="exception">异常对象</param>
        /// <param name="callerMemberName">调用方法名（自动填充）</param>
        /// <param name="callerFilePath">调用文件路径（自动填充）</param>
        /// <param name="callerLineNumber">调用行号（自动填充）</param>
        public static void Error(
            string module,
            string operation,
            Exception exception,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var message = FormatMessage(
                LogLevel.Error,
                module,
                operation,
                exception,
                callerMemberName,
                callerFilePath,
                callerLineNumber);

            WriteLog(message, "error.log");
        }

        /// <summary>
        /// 记录错误（无异常）
        /// </summary>
        public static void Error(
            string module,
            string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var logMessage = FormatMessage(
                LogLevel.Error,
                module,
                message,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber);

            WriteLog(logMessage, "error.log");
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        public static void Warning(
            string module,
            string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var logMessage = FormatMessage(
                LogLevel.Warning,
                module,
                message,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber);

            WriteLog(logMessage, "app.log");
        }

        /// <summary>
        /// 记录信息
        /// </summary>
        public static void Info(
            string module,
            string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            var logMessage = FormatMessage(
                LogLevel.Info,
                module,
                message,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber);

            WriteLog(logMessage, "app.log");
        }

        /// <summary>
        /// 记录调试信息
        /// </summary>
        public static void Debug(
            string module,
            string message,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
#if DEBUG
            var logMessage = FormatMessage(
                LogLevel.Debug,
                module,
                message,
                null,
                callerMemberName,
                callerFilePath,
                callerLineNumber);

            // 调试信息只输出到控制台，不写文件
            System.Diagnostics.Debug.WriteLine(logMessage);
#endif
        }

        /// <summary>
        /// 格式化日志消息
        /// </summary>
        private static string FormatMessage(
            LogLevel level,
            string module,
            string message,
            Exception? exception,
            string callerMemberName,
            string callerFilePath,
            int callerLineNumber)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var fileName = Path.GetFileName(callerFilePath);
            var location = $"{fileName}:{callerLineNumber} ({callerMemberName})";

            var formattedMessage = $"[{timestamp}] [{level}] [{module}] {message}";

            if (exception != null)
            {
                formattedMessage += $"\n异常类型: {exception.GetType().FullName}";
                formattedMessage += $"\n异常消息: {exception.Message}";
                formattedMessage += $"\n堆栈跟踪:\n{exception.StackTrace}";
                
                if (exception.InnerException != null)
                {
                    formattedMessage += $"\n\n内部异常: {exception.InnerException.GetType().FullName}";
                    formattedMessage += $"\n内部异常消息: {exception.InnerException.Message}";
                    formattedMessage += $"\n内部异常堆栈:\n{exception.InnerException.StackTrace}";
                }
            }

            formattedMessage += $"\n位置: {location}\n";

            return formattedMessage;
        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        private static void WriteLog(string message, string fileName)
        {
            // 总是输出到调试控制台
            System.Diagnostics.Debug.WriteLine(message);

            // 尝试写入文件
            try
            {
                EnsureLogDirectory();

                if (_logDirectory == null)
                {
                    return; // 无法创建日志目录，只输出控制台
                }

                var logFilePath = Path.Combine(_logDirectory, fileName);

                lock (_fileLock)
                {
                    File.AppendAllText(logFilePath, message + Environment.NewLine);
                }
            }
            catch
            {
                // 写入日志失败不应影响程序运行
                // 已经输出到控制台，不再处理
            }
        }

        /// <summary>
        /// 清理旧日志（保留最近 N 天）
        /// </summary>
        public static void CleanupOldLogs(int keepDays = 7)
        {
            try
            {
                EnsureLogDirectory();

                if (_logDirectory == null || !Directory.Exists(_logDirectory))
                {
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-keepDays);
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            System.Diagnostics.Debug.WriteLine($"[LogService] Deleted old log file: {fileInfo.Name}");
                        }
                        catch
                        {
                            // 删除失败，忽略
                        }
                    }
                }
            }
            catch
            {
                // 清理失败不影响程序运行
            }
        }

        /// <summary>
        /// 获取日志目录路径
        /// </summary>
        public static string? GetLogDirectory()
        {
            EnsureLogDirectory();
            return _logDirectory;
        }
    }
}

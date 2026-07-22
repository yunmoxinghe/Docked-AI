using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Docked_AI.Features.UnifiedCalls.Logging;

namespace Docked_AI.Features.UnifiedCalls.AsyncSafety
{
    /// <summary>
    /// 异步安全 Helper
    /// 
    /// 【功能】
    /// 1. 包装 async void 事件处理器,避免异常逃逸到 XAML 未处理异常
    /// 2. 统一记录异步操作中的异常
    /// 3. 保持 WinUI 事件处理器签名要求
    /// 
    /// 【使用方法】
    /// // 在事件处理器中使用
    /// private void Button_Click(object sender, RoutedEventArgs e)
    /// {
    ///     AsyncSafety.Run(ButtonClickAsync, "ModuleName", "ButtonClick");
    /// }
    /// 
    /// private async Task ButtonClickAsync()
    /// {
    ///     // 实际异步逻辑
    /// }
    /// 
    /// 【设计原理】
    /// - 事件处理器保留 WinUI 要求的签名 (void)
    /// - 内部逻辑移到 async Task 方法
    /// - 异常捕获后记录日志,不向上传播
    /// - 避免 async void continuation 异常绕过业务代码
    /// </summary>
    internal static class AsyncSafety
    {
        /// <summary>
        /// 安全执行异步操作 (async void 入口)
        /// </summary>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="module">模块名称 (用于日志记录)</param>
        /// <param name="operation">操作描述 (用于日志记录)</param>
        /// <param name="callerMemberName">调用方法名 (自动填充)</param>
        /// <param name="callerFilePath">调用文件路径 (自动填充)</param>
        /// <param name="callerLineNumber">调用行号 (自动填充)</param>
        public static async void Run(
            Func<Task> action,
            string module,
            string operation,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            try
            {
                await action().ConfigureAwait(true); // WinUI 通常需要回到 UI 线程
            }
            catch (Exception ex)
            {
                LogService.Error(
                    module,
                    $"异步操作失败: {operation}",
                    ex,
                    callerMemberName,
                    callerFilePath,
                    callerLineNumber);
            }
        }

        /// <summary>
        /// 安全执行异步操作 (async Task 入口)
        /// </summary>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="module">模块名称 (用于日志记录)</param>
        /// <param name="operation">操作描述 (用于日志记录)</param>
        /// <param name="callerMemberName">调用方法名 (自动填充)</param>
        /// <param name="callerFilePath">调用文件路径 (自动填充)</param>
        /// <param name="callerLineNumber">调用行号 (自动填充)</param>
        /// <returns>可等待的 Task</returns>
        public static async Task RunTask(
            Func<Task> action,
            string module,
            string operation,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogService.Error(
                    module,
                    $"异步操作失败: {operation}",
                    ex,
                    callerMemberName,
                    callerFilePath,
                    callerLineNumber);

                // Task 版本重新抛出异常,允许调用方决定如何处理
                throw;
            }
        }

        /// <summary>
        /// 安全执行带返回值的异步操作 (async Task&lt;T&gt; 入口)
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="module">模块名称 (用于日志记录)</param>
        /// <param name="operation">操作描述 (用于日志记录)</param>
        /// <param name="defaultValue">异常时的默认返回值</param>
        /// <param name="callerMemberName">调用方法名 (自动填充)</param>
        /// <param name="callerFilePath">调用文件路径 (自动填充)</param>
        /// <param name="callerLineNumber">调用行号 (自动填充)</param>
        /// <returns>操作结果或默认值</returns>
        public static async Task<T> RunTask<T>(
            Func<Task<T>> action,
            string module,
            string operation,
            T defaultValue,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            try
            {
                return await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogService.Error(
                    module,
                    $"异步操作失败: {operation}",
                    ex,
                    callerMemberName,
                    callerFilePath,
                    callerLineNumber);

                return defaultValue;
            }
        }

        /// <summary>
        /// 安全执行 DispatcherQueue 回调
        /// </summary>
        /// <param name="dispatcherQueue">DispatcherQueue 实例</param>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="module">模块名称 (用于日志记录)</param>
        /// <param name="operation">操作描述 (用于日志记录)</param>
        /// <param name="callerMemberName">调用方法名 (自动填充)</param>
        /// <param name="callerFilePath">调用文件路径 (自动填充)</param>
        /// <param name="callerLineNumber">调用行号 (自动填充)</param>
        /// <returns>是否成功排队执行</returns>
        public static bool TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue,
            Func<Task> action,
            string module,
            string operation,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            if (dispatcherQueue == null)
            {
                LogService.Warning(
                    module,
                    $"DispatcherQueue 为 null,无法执行: {operation}",
                    callerMemberName,
                    callerFilePath,
                    callerLineNumber);
                return false;
            }

            return dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    LogService.Error(
                        module,
                        $"DispatcherQueue 异步操作失败: {operation}",
                        ex,
                        callerMemberName,
                        callerFilePath,
                        callerLineNumber);
                }
            });
        }
    }
}

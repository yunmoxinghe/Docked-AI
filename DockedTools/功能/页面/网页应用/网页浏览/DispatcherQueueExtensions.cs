using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// DispatcherQueue 扩展方法
    /// </summary>
    public static class DispatcherQueueExtensions
    {
        /// <summary>
        /// 异步执行操作在 UI 线程上
        /// </summary>
        public static Task EnqueueAsync(this DispatcherQueue dispatcher, Func<Task> function, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            var tcs = new TaskCompletionSource<bool>();

            bool enqueued = dispatcher.TryEnqueue(priority, async () =>
            {
                try
                {
                    await function();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!enqueued)
            {
                tcs.SetException(new InvalidOperationException("Failed to enqueue operation on DispatcherQueue"));
            }

            return tcs.Task;
        }

        /// <summary>
        /// 异步执行操作在 UI 线程上（同步版本）
        /// </summary>
        public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            var tcs = new TaskCompletionSource<bool>();

            bool enqueued = dispatcher.TryEnqueue(priority, () =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!enqueued)
            {
                tcs.SetException(new InvalidOperationException("Failed to enqueue operation on DispatcherQueue"));
            }

            return tcs.Task;
        }
    }
}

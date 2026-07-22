using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Docked_AI.Features.Debug
{
    /// <summary>
    /// 线程死锁检测器
    /// 定期检测线程状态，当发现可疑情况时输出详细信息
    /// 
    /// 【使用方法】
    /// 在 App.xaml.cs 的 OnLaunched 中启动：
    /// ThreadDeadlockDetector.Start();
    /// 
    /// 【检测内容】
    /// 1. 线程总数异常增长
    /// 2. 等待状态的线程过多
    /// 3. UI 线程无响应
    /// </summary>
    public static class ThreadDeadlockDetector
    {
        private static CancellationTokenSource? _cts;
        private static Task? _monitorTask;
        private static int _baselineThreadCount;
        private static DateTime _lastLogTime = DateTime.MinValue;

        /// <summary>
        /// 启动死锁检测器
        /// </summary>
        /// <param name="checkInterval">检测间隔（默认 5 秒）</param>
        public static void Start(TimeSpan? checkInterval = null)
        {
            if (_monitorTask != null)
            {
                System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] Already started");
                return;
            }

            _baselineThreadCount = Process.GetCurrentProcess().Threads.Count;
            System.Diagnostics.Debug.WriteLine($"[ThreadDeadlockDetector] Baseline thread count: {_baselineThreadCount}");

            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(checkInterval ?? TimeSpan.FromSeconds(5), _cts.Token));
            
            System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] Started");
        }

        /// <summary>
        /// 停止死锁检测器
        /// </summary>
        public static void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _monitorTask = null;
            System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] Stopped");
        }

        /// <summary>
        /// 监控循环
        /// </summary>
        private static async Task MonitorLoop(TimeSpan checkInterval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(checkInterval, cancellationToken);
                    CheckThreadHealth();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ThreadDeadlockDetector] Monitor error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查线程健康状态
        /// </summary>
        private static void CheckThreadHealth()
        {
            var process = Process.GetCurrentProcess();
            var threads = process.Threads;
            var threadCount = threads.Count;

            // 统计各种状态的线程
            int waitingThreads = 0;
            int runningThreads = 0;
            int otherThreads = 0;

            foreach (ProcessThread thread in threads)
            {
                switch (thread.ThreadState)
                {
                    case System.Diagnostics.ThreadState.Wait:
                        waitingThreads++;
                        break;
                    case System.Diagnostics.ThreadState.Running:
                        runningThreads++;
                        break;
                    default:
                        otherThreads++;
                        break;
                }
            }

            // 检测异常情况
            bool suspicious = false;
            var report = new StringBuilder();
            report.AppendLine($"[ThreadDeadlockDetector] Thread Health Report ({DateTime.Now:HH:mm:ss})");
            report.AppendLine($"  Total threads: {threadCount} (baseline: {_baselineThreadCount})");
            report.AppendLine($"  Running: {runningThreads}");
            report.AppendLine($"  Waiting: {waitingThreads}");
            report.AppendLine($"  Other: {otherThreads}");

            // 异常1：线程数暴增（超过基线 50%）
            if (threadCount > _baselineThreadCount * 1.5)
            {
                suspicious = true;
                report.AppendLine($"  ⚠️ WARNING: Thread count increased by {((threadCount - _baselineThreadCount) * 100.0 / _baselineThreadCount):F0}%");
            }

            // 异常2：等待线程过多（超过 80%）
            if (threadCount > 0 && (waitingThreads * 100.0 / threadCount) > 80)
            {
                suspicious = true;
                report.AppendLine($"  ⚠️ WARNING: {waitingThreads * 100.0 / threadCount:F0}% threads are waiting (possible deadlock)");
            }

            // 异常3：CPU 使用率异常低但线程很多
            try
            {
                process.Refresh();
                var cpuUsage = process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
                if (threadCount > 20 && cpuUsage < 100)
                {
                    suspicious = true;
                    report.AppendLine($"  ⚠️ WARNING: Many threads but low CPU usage (possible blocking)");
                }
            }
            catch
            {
                // 忽略 CPU 统计错误
            }

            // 只在发现可疑情况时输出完整报告
            if (suspicious)
            {
                System.Diagnostics.Debug.WriteLine(report.ToString());
                
                // 输出详细线程栈信息（每 30 秒最多一次，避免日志爆炸）
                if ((DateTime.Now - _lastLogTime).TotalSeconds > 30)
                {
                    _lastLogTime = DateTime.Now;
                    DumpDetailedThreadInfo();
                }
            }
        }

        /// <summary>
        /// 输出详细线程信息（用于死锁分析）
        /// </summary>
        private static void DumpDetailedThreadInfo()
        {
            System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] === Detailed Thread Dump ===");
            
            var process = Process.GetCurrentProcess();
            var threads = process.Threads.Cast<ProcessThread>()
                                .OrderByDescending(t => t.TotalProcessorTime)
                                .Take(10); // 只显示 CPU 时间最长的前 10 个线程

            foreach (var thread in threads)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"  Thread #{thread.Id}:");
                    System.Diagnostics.Debug.WriteLine($"    State: {thread.ThreadState}");
                    System.Diagnostics.Debug.WriteLine($"    Priority: {thread.PriorityLevel}");
                    System.Diagnostics.Debug.WriteLine($"    CPU Time: {thread.TotalProcessorTime.TotalMilliseconds:F0}ms");
                    
                    if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
                    {
                        System.Diagnostics.Debug.WriteLine($"    Wait Reason: {thread.WaitReason}");
                    }
                }
                catch
                {
                    // 某些线程信息可能无法访问
                }
            }

            System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] === End of Thread Dump ===");
        }

        /// <summary>
        /// 手动触发线程健康检查并输出报告
        /// </summary>
        public static void TriggerHealthCheck()
        {
            System.Diagnostics.Debug.WriteLine("[ThreadDeadlockDetector] Manual health check triggered");
            CheckThreadHealth();
            DumpDetailedThreadInfo();
        }
    }
}

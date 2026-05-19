using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// UI 线程看门狗 - 监控主 UI 线程健康状态
    /// 
    /// 【核心功能】
    /// 1. 定期向主 UI 线程发送心跳检测任务
    /// 2. 如果 UI 线程在超时时间内未响应，判定为卡死
    /// 3. 卡死时自动显示紧急恢复对话框（独立进程）
    /// 4. 提供强制退出、重启应用等紧急操作
    /// 
    /// 【设计原理】
    /// - 看门狗运行在独立的后台线程上
    /// - 使用 DispatcherQueue.TryEnqueue 发送心跳任务
    /// - 如果任务在超时时间内未完成，触发卡死检测
    /// - 紧急对话框使用独立进程（避免被卡死的主进程影响）
    /// </summary>
    public class UIThreadWatchdog : IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _timeout;
        private CancellationTokenSource? _cts;
        private Task? _watchdogTask;
        private bool _disposed;
        private DateTime _lastHeartbeat;
        private readonly object _heartbeatLock = new object();

        /// <summary>
        /// UI 线程卡死事件
        /// </summary>
        public event EventHandler<UIThreadFrozenEventArgs>? UIThreadFrozen;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dispatcherQueue">主 UI 线程的 DispatcherQueue</param>
        /// <param name="checkInterval">检测间隔（默认 2 秒）</param>
        /// <param name="timeout">超时时间（默认 5 秒）</param>
        public UIThreadWatchdog(
            DispatcherQueue dispatcherQueue,
            TimeSpan? checkInterval = null,
            TimeSpan? timeout = null)
        {
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            _checkInterval = checkInterval ?? TimeSpan.FromSeconds(2);
            _timeout = timeout ?? TimeSpan.FromSeconds(5);
            _lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>
        /// 启动看门狗
        /// </summary>
        public void Start()
        {
            if (_watchdogTask != null)
            {
                Debug.WriteLine("[UIThreadWatchdog] Already started");
                return;
            }

            _cts = new CancellationTokenSource();
            _watchdogTask = Task.Run(() => WatchdogLoop(_cts.Token));
            Debug.WriteLine("[UIThreadWatchdog] Started");
        }

        /// <summary>
        /// 停止看门狗
        /// </summary>
        public void Stop()
        {
            if (_cts == null || _watchdogTask == null)
            {
                return;
            }

            Debug.WriteLine("[UIThreadWatchdog] Stopping...");
            _cts.Cancel();
            _watchdogTask.Wait(TimeSpan.FromSeconds(1));
            _cts.Dispose();
            _cts = null;
            _watchdogTask = null;
            Debug.WriteLine("[UIThreadWatchdog] Stopped");
        }

        /// <summary>
        /// 看门狗循环
        /// </summary>
        private async Task WatchdogLoop(CancellationToken cancellationToken)
        {
            Debug.WriteLine("[UIThreadWatchdog] Watchdog loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 等待检测间隔
                    await Task.Delay(_checkInterval, cancellationToken);

                    // 发送心跳检测任务到 UI 线程
                    bool heartbeatReceived = false;
                    var heartbeatTask = Task.Run(() =>
                    {
                        var enqueued = _dispatcherQueue.TryEnqueue(() =>
                        {
                            // UI 线程响应心跳
                            lock (_heartbeatLock)
                            {
                                _lastHeartbeat = DateTime.UtcNow;
                                heartbeatReceived = true;
                            }
                        });

                        if (!enqueued)
                        {
                            Debug.WriteLine("[UIThreadWatchdog] Failed to enqueue heartbeat task");
                        }
                    });

                    // 等待心跳响应（带超时）
                    await Task.Delay(_timeout, cancellationToken);

                    // 检查是否收到心跳
                    lock (_heartbeatLock)
                    {
                        var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeat;
                        
                        if (timeSinceLastHeartbeat > _timeout)
                        {
                            // UI 线程卡死
                            Debug.WriteLine($"[UIThreadWatchdog] ⚠️ UI thread frozen! Time since last heartbeat: {timeSinceLastHeartbeat.TotalSeconds:F1}s");
                            OnUIThreadFrozen(timeSinceLastHeartbeat);
                        }
                        else if (!heartbeatReceived)
                        {
                            Debug.WriteLine($"[UIThreadWatchdog] ⚠️ Heartbeat not received yet, but within timeout ({timeSinceLastHeartbeat.TotalSeconds:F1}s)");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UIThreadWatchdog] Error in watchdog loop: {ex.Message}");
                }
            }

            Debug.WriteLine("[UIThreadWatchdog] Watchdog loop exited");
        }

        /// <summary>
        /// 触发 UI 线程卡死事件
        /// </summary>
        private void OnUIThreadFrozen(TimeSpan frozenDuration)
        {
            var args = new UIThreadFrozenEventArgs(frozenDuration);
            UIThreadFrozen?.Invoke(this, args);

            // 如果没有订阅者，执行默认操作
            if (UIThreadFrozen == null || UIThreadFrozen.GetInvocationList().Length == 0)
            {
                HandleFrozenUIThreadDefault(frozenDuration);
            }
        }

        /// <summary>
        /// 默认的 UI 线程卡死处理逻辑
        /// </summary>
        private void HandleFrozenUIThreadDefault(TimeSpan frozenDuration)
        {
            Debug.WriteLine($"[UIThreadWatchdog] Handling frozen UI thread (duration: {frozenDuration.TotalSeconds:F1}s)");

            // 显示 Windows 通知（不依赖 UI 线程）
            ShowWindowsNotification(
                "Docked AI 无响应",
                $"应用程序已无响应 {frozenDuration.TotalSeconds:F0} 秒。\n右键托盘图标可强制退出。");

            // 可选：自动重启应用（谨慎使用）
            // RestartApplication();
        }

        /// <summary>
        /// 显示 Windows 系统通知
        /// </summary>
        private void ShowWindowsNotification(string title, string message)
        {
            try
            {
                // 使用 PowerShell 显示 Windows 通知（不依赖 UI 线程）
                var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$template = @""
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{title}</text>
            <text>{message}</text>
        </binding>
    </visual>
</toast>
""@

$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = New-Object Windows.UI.Notifications.ToastNotification $xml
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Docked AI').Show($toast)
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UIThreadWatchdog] Failed to show notification: {ex.Message}");
            }
        }

        /// <summary>
        /// 重启应用程序
        /// </summary>
        private void RestartApplication()
        {
            try
            {
                Debug.WriteLine("[UIThreadWatchdog] Restarting application...");
                
                // 启动新实例
                var currentProcess = Process.GetCurrentProcess();
                Process.Start(currentProcess.MainModule?.FileName ?? "Docked AI.exe");
                
                // 强制退出当前进程
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UIThreadWatchdog] Failed to restart application: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            Stop();
        }
    }

    /// <summary>
    /// UI 线程卡死事件参数
    /// </summary>
    public class UIThreadFrozenEventArgs : EventArgs
    {
        public TimeSpan FrozenDuration { get; }

        public UIThreadFrozenEventArgs(TimeSpan frozenDuration)
        {
            FrozenDuration = frozenDuration;
        }
    }
}

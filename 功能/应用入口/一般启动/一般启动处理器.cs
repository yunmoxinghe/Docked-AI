using System;
using Microsoft.UI.Xaml;
using Docked_AI.Features.Tray;

namespace Docked_AI.Features.AppEntry.NormalLaunch
{
    /// <summary>
    /// 处理应用的一般启动逻辑
    /// </summary>
    public class NormalLaunchHandler
    {
        private readonly App _app;
        private TrayIconManager? _trayIconManager;

        public NormalLaunchHandler(App app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        /// <summary>
        /// 处理一般启动
        /// </summary>
        /// <param name="exitCallback">退出回调</param>
        /// <param name="shouldShowWindow">是否应该显示主窗口（从图标启动时为 true）</param>
        public void Handle(Action exitCallback, bool shouldShowWindow = false)
        {
            // Initialize the tray icon manager
            _trayIconManager = new TrayIconManager(null, exitCallback);
            _trayIconManager.Initialize();

            // 如果是从图标启动（非自启动），自动显示主窗口
            if (shouldShowWindow)
            {
                System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] Auto-showing main window on icon launch");
                
                // 使用 DispatcherQueue 确保在 UI 线程上执行，并稍微延迟以确保托盘完全初始化
                var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                dispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        // 再次延迟一小段时间，确保所有初始化完成
                        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                        {
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                _trayIconManager?.ShowMainWindow();
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NormalLaunchHandler] Error auto-showing window: {ex.Message}");
                    }
                });
            }
        }

        public TrayIconManager? TrayIconManager => _trayIconManager;
    }
}

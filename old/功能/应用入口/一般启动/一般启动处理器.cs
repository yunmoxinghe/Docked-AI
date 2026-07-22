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
            System.Diagnostics.Debug.WriteLine($"[NormalLaunchHandler] Handle called, shouldShowWindow={shouldShowWindow}");
            
            // Initialize the tray icon manager
            _trayIconManager = new TrayIconManager(null, exitCallback);
            _trayIconManager.Initialize();

            System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] Tray icon manager initialized");

            // 如果是从图标启动（非自启动），自动显示主窗口
            if (shouldShowWindow)
            {
                System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] Auto-showing main window on icon launch");
                
                // 使用更可靠的方式确保窗口显示
                // 直接在当前线程调用，避免多层异步嵌套导致的时序问题
                try
                {
                    // 使用短暂延迟确保托盘初始化完成，但使用同步方式等待
                    var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                    if (dispatcherQueue != null)
                    {
                        // 使用 High 优先级确保尽快执行
                        bool enqueued = dispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                        {
                            System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] Executing ShowMainWindow on UI thread");
                            
                            if (_trayIconManager != null)
                            {
                                _trayIconManager.ShowMainWindow();
                                System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] ShowMainWindow called successfully");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] ERROR: TrayIconManager is null!");
                            }
                        });
                        
                        if (!enqueued)
                        {
                            System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] WARNING: Failed to enqueue ShowMainWindow");
                        }
                    }
                    else
                    {
                        // 降级处理：如果无法获取 DispatcherQueue，直接调用
                        System.Diagnostics.Debug.WriteLine("[NormalLaunchHandler] WARNING: DispatcherQueue is null, calling ShowMainWindow directly");
                        _trayIconManager?.ShowMainWindow();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NormalLaunchHandler] ERROR auto-showing window: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[NormalLaunchHandler] Stack trace: {ex.StackTrace}");
                }
            }
        }

        public TrayIconManager? TrayIconManager => _trayIconManager;
    }
}

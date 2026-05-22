using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 独立 UI 线程菜单宿主 - 在独立的 UI 线程上显示托盘菜单
    /// 
    /// 【核心设计】
    /// 1. 创建独立的 STA 线程，运行独立的 DispatcherQueue
    /// 2. 在独立线程上创建隐藏窗口，用于承载 WinUI 菜单
    /// 3. 托盘菜单在独立线程上显示，不受主 UI 线程阻塞影响
    /// 4. 菜单事件回调通过委托传递到主线程
    /// 
    /// 【关键优势】
    /// - 主窗口卡死时，托盘菜单仍然可以正常显示和响应
    /// - 使用 WinUI 菜单，保持原有的样式和触摸支持
    /// - 菜单操作（退出、重启）可以在独立线程上执行
    /// </summary>
    public class IndependentUIThreadMenuHost : IDisposable
    {
        private Thread? _menuThread;
        private DispatcherQueue? _dispatcherQueue;
        private Window? _menuHostWindow;
        private bool _disposed;
        private ManualResetEventSlim _threadStarted = new ManualResetEventSlim(false);
        private TaskCompletionSource<bool>? _menuClosedTcs;

        /// <summary>
        /// 初始化独立 UI 线程
        /// </summary>
        public void Initialize()
        {
            if (_menuThread != null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[IndependentUIThreadMenuHost] Initializing...");

            // 创建独立的 STA 线程
            _menuThread = new Thread(MenuThreadProc)
            {
                Name = "TrayMenuUIThread",
                IsBackground = false
            };
            _menuThread.SetApartmentState(ApartmentState.STA);
            _menuThread.Start();

            // 等待线程启动完成（使用超时避免无限等待）
            if (!_threadStarted.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Menu thread initialization timed out after 5 seconds");
            }
            System.Diagnostics.Debug.WriteLine("[IndependentUIThreadMenuHost] Initialized successfully");
        }

        /// <summary>
        /// 菜单线程入口函数
        /// </summary>
        private void MenuThreadProc()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MenuThread] Starting...");

                // 初始化 DispatcherQueue（WinUI 3 需要）
                var controller = DispatcherQueueController.CreateOnCurrentThread();
                _dispatcherQueue = controller.DispatcherQueue;

                // 创建隐藏窗口用于承载菜单
                _menuHostWindow = new Window
                {
                    Content = new Grid()
                };

                // 配置窗口为完全隐藏
                var appWindow = _menuHostWindow.AppWindow;
                appWindow.IsShownInSwitchers = false;
                appWindow.MoveAndResize(new RectInt32(-32000, -32000, 1, 1));

                // 激活窗口（必须激活才能显示菜单）
                _menuHostWindow.Activate();

                // 隐藏窗口
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_menuHostWindow);
                ShowWindow(hwnd, SW_HIDE);

                System.Diagnostics.Debug.WriteLine("[MenuThread] Menu host window created");

                // 通知主线程初始化完成
                _threadStarted.Set();

                // 进入消息循环
                System.Diagnostics.Debug.WriteLine("[MenuThread] Entering message loop...");
                
                // 使用 DispatcherQueue 的消息循环
                var frame = new DispatcherQueueSynchronizationContext(_dispatcherQueue);
                SynchronizationContext.SetSynchronizationContext(frame);
                
                // 保持线程运行
                // 使用 ManualResetEventSlim 代替 Thread.Sleep 循环，更高效
                var shutdownEvent = new ManualResetEventSlim(false);
                while (!_disposed)
                {
                    shutdownEvent.Wait(100); // 等待 100ms 或直到信号
                }
                shutdownEvent.Dispose();

                System.Diagnostics.Debug.WriteLine("[MenuThread] Message loop exited");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuThread] ERROR: {ex.Message}");
                _threadStarted.Set();
            }
        }

        /// <summary>
        /// 显示菜单
        /// </summary>
        /// <param name="menuFactory">菜单工厂函数（在菜单线程上调用）</param>
        /// <param name="x">菜单 X 坐标</param>
        /// <param name="y">菜单 Y 坐标</param>
        public Task ShowMenuAsync(Func<MenuFlyout> menuFactory, int x, int y)
        {
            if (_dispatcherQueue == null || _menuHostWindow == null)
            {
                throw new InvalidOperationException("Menu host not initialized");
            }

            _menuClosedTcs = new TaskCompletionSource<bool>();

            // 在菜单线程上显示菜单
            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuThread] Showing menu at ({x}, {y})");

                    // 创建菜单
                    var menu = menuFactory();

                    // 订阅菜单关闭事件
                    menu.Closed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("[MenuThread] Menu closed");
                        _menuClosedTcs?.TrySetResult(true);
                    };

                    // 移动窗口到菜单位置
                    _menuHostWindow.AppWindow.MoveAndResize(
                        new RectInt32(x, y, 1, 1),
                        Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                            new PointInt32(x, y), 
                            Microsoft.UI.Windowing.DisplayAreaFallback.Primary));

                    // 显示窗口（必须显示才能显示菜单）
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_menuHostWindow);
                    ShowWindow(hwnd, SW_SHOW);
                    SetForegroundWindow(hwnd);

                    // 显示菜单
                    var grid = (Grid)_menuHostWindow.Content;
                    menu.ShowAt(grid, new FlyoutShowOptions
                    {
                        Placement = FlyoutPlacementMode.Top,
                        ShowMode = FlyoutShowMode.Standard
                    });

                    System.Diagnostics.Debug.WriteLine("[MenuThread] Menu shown successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuThread] ShowMenu error: {ex.Message}");
                    _menuClosedTcs?.TrySetException(ex);
                }
            });

            return _menuClosedTcs.Task;
        }

        /// <summary>
        /// 在菜单线程上执行操作
        /// </summary>
        public void ExecuteOnMenuThread(Action action)
        {
            if (_dispatcherQueue == null)
            {
                throw new InvalidOperationException("Menu host not initialized");
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuThread] ExecuteOnMenuThread error: {ex.Message}");
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            System.Diagnostics.Debug.WriteLine("[IndependentUIThreadMenuHost] Disposing...");

            // 关闭菜单窗口
            if (_menuHostWindow != null && _dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _menuHostWindow?.Close();
                });
            }

            // 等待线程退出
            if (_menuThread != null && !_menuThread.Join(3000))
            {
                System.Diagnostics.Debug.WriteLine("[IndependentUIThreadMenuHost] WARNING: Menu thread did not exit in time");
            }

            _threadStarted.Dispose();
            System.Diagnostics.Debug.WriteLine("[IndependentUIThreadMenuHost] Disposed");
        }

        // Win32 API
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}

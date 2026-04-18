using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Docked_AI.Features.AppEntry;
using Docked_AI.Features.Tray;
using Docked_AI.Features.AppEntry.NormalLaunch;
using Docked_AI.Features.AppEntry.AutoLaunch;
using Docked_AI.Features.AppEntry.ShareLaunch;
using Docked_AI.Features.MainWindow.Entry;
using Windows.Graphics;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;

namespace Docked_AI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private Window? _keepAliveWindow;
        private TrayIconManager? _trayIconManager;
        private AppInstance? _mainInstance;
        
        // Launch handlers
        private NormalLaunchHandler? _normalLaunchHandler;
        private AutoLaunchHandler? _autoLaunchHandler;
        private ShareLaunchHandler? _shareLaunchHandler;

        /// <summary>
        /// 获取主窗口实例（用于内部访问）
        /// </summary>
        public Window? MainWindow => _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("[App] OnLaunched called");
            
            try
            {
                // 使用 Windows App SDK 的 AppInstance API 实现单实例
                // 这是官方推荐的最佳实践，比 Mutex + IPC 更简洁、更可靠
                _mainInstance = AppInstance.FindOrRegisterForKey("DockedAI_MainInstance");
                System.Diagnostics.Debug.WriteLine($"[App] AppInstance registered, IsCurrent={_mainInstance.IsCurrent}");

                if (!_mainInstance.IsCurrent)
                {
                    // 已有实例在运行 → 重定向激活到主实例
                    var currentInstance = AppInstance.GetCurrent();
                    var activationArgs = currentInstance.GetActivatedEventArgs();
                    
                    System.Diagnostics.Debug.WriteLine("[App] Redirecting activation to existing instance");
                    
                    // 异步重定向激活（不阻塞）
                    _ = _mainInstance.RedirectActivationToAsync(activationArgs).AsTask().ContinueWith(_ =>
                    {
                        // 重定向完成后，退出当前实例
                        System.Diagnostics.Debug.WriteLine("[App] Activation redirected, exiting current instance");
                        System.Diagnostics.Process.GetCurrentProcess().Kill();
                    });
                    
                    return;
                }

                // 当前是主实例 → 订阅激活事件（处理后续的重定向激活）
                _mainInstance.Activated += OnActivated;
                System.Diagnostics.Debug.WriteLine("[App] This is the main instance, subscribed to Activated event");

                // Initialize handlers
                _normalLaunchHandler = new NormalLaunchHandler(this);
                _autoLaunchHandler = new AutoLaunchHandler(this);
                _shareLaunchHandler = new ShareLaunchHandler(this);
                System.Diagnostics.Debug.WriteLine("[App] Launch handlers initialized");

                var activationArgs2 = AppInstance.GetCurrent().GetActivatedEventArgs();
                System.Diagnostics.Debug.WriteLine($"[App] Activation kind: {activationArgs2?.Kind}");

                // ShareTarget activation should always proceed
                if (activationArgs2?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.ShareTarget)
                {
                    System.Diagnostics.Debug.WriteLine("[App] Handling ShareTarget activation");
                    HandleShareTargetActivation(activationArgs2.Data as ShareTargetActivatedEventArgs);
                    return;
                }

                // Check if this is an auto-launch scenario
                bool isAutoLaunch = _autoLaunchHandler.IsAutoLaunch();
                System.Diagnostics.Debug.WriteLine($"[App] IsAutoLaunch: {isAutoLaunch}");
                
                if (isAutoLaunch)
                {
                    System.Diagnostics.Debug.WriteLine("[App] Handling auto-launch");
                    _ = _autoLaunchHandler.HandleAsync();
                }

                // Handle normal launch
                // 从图标启动时（非自启动），自动显示主窗口
                bool shouldShowWindow = !isAutoLaunch;
                System.Diagnostics.Debug.WriteLine($"[App] Calling NormalLaunchHandler.Handle with shouldShowWindow={shouldShowWindow}");
                
                _normalLaunchHandler.Handle(ExitApplication, shouldShowWindow: shouldShowWindow);
                _trayIconManager = _normalLaunchHandler.TrayIconManager;
                
                System.Diagnostics.Debug.WriteLine("[App] Creating keep-alive window");
                EnsureKeepAliveWindow();

                System.Diagnostics.Debug.WriteLine("[App] OnLaunched completed successfully");
                
                // 优化说明：
                // - 使用 Windows App SDK 的 AppInstance API（官方推荐）
                // - 自启动时：不显示窗口，只在托盘运行
                // - 图标启动时：自动显示主窗口，提供更好的用户体验
                // - 再次点击图标时：通过 RedirectActivationToAsync 激活主实例
                // - 响应速度：< 1ms（系统级 API，最快！）
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] CRITICAL ERROR in OnLaunched: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");
                LogException("OnLaunched", ex);
                throw;
            }
        }

        /// <summary>
        /// 处理激活事件（当其他实例重定向激活到主实例时触发）
        /// </summary>
        private void OnActivated(object? sender, AppActivationArguments args)
        {
            System.Diagnostics.Debug.WriteLine($"[App] OnActivated called, Kind: {args.Kind}");

            // 在 UI 线程上执行
            if (_keepAliveWindow?.DispatcherQueue != null)
            {
                _keepAliveWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                {
                    System.Diagnostics.Debug.WriteLine("[App] Activating window from another instance click");
                    
                    // 显示主窗口
                    _trayIconManager?.ShowMainWindow();
                });
            }
            else
            {
                // 降级处理：直接调用
                _trayIconManager?.ShowMainWindow();
            }
        }

        private async void HandleShareTargetActivation(ShareTargetActivatedEventArgs? shareArgs)
        {
            if (_shareLaunchHandler == null)
            {
                _shareLaunchHandler = new ShareLaunchHandler(this);
            }

            if (_window == null)
            {
                _window = MainWindowFactory.Create();
            }

            await _shareLaunchHandler.HandleAsync(shareArgs, _window);
        }

        public void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        public void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void ShowMainWindow()
        {
            _window = MainWindowFactory.GetOrCreate(_window);
            _window.Activate();
        }

        private void OnAppExit(object sender, object e)
        {
            _trayIconManager?.Dispose();
            
            // 取消订阅激活事件
            if (_mainInstance != null)
            {
                _mainInstance.Activated -= OnActivated;
            }
        }

        private void ExitApplication()
        {
            try
            {
                _trayIconManager?.Dispose();
                _trayIconManager = null;
                
                // 取消订阅激活事件
                if (_mainInstance != null)
                {
                    _mainInstance.Activated -= OnActivated;
                    _mainInstance = null;
                }
            }
            finally
            {
                Exit();
            }
        }

        private void EnsureKeepAliveWindow()
        {
            if (_keepAliveWindow != null)
            {
                return;
            }

            // WinUI desktop apps may terminate quickly when no window is created.
            // Keep a hidden host window alive for tray-only mode.
            _keepAliveWindow = new Window
            {
                Content = new Grid()
            };

            // Keep the host window fully out of user sight before activation.
            var keepAliveAppWindow = _keepAliveWindow.AppWindow;
            keepAliveAppWindow.IsShownInSwitchers = false;
            keepAliveAppWindow.MoveAndResize(new RectInt32(-32000, -32000, 1, 1));

            _keepAliveWindow.Activate();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_keepAliveWindow);
            if (hwnd != IntPtr.Zero)
            {
                AppEntryWin32Api.ShowWindow(hwnd, AppEntryWin32Api.SW_HIDE);
            }
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogException("App.UnhandledException", e.Exception);
        }

        private void CurrentDomain_UnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("AppDomain.UnhandledException", ex);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void LogException(string source, Exception ex)
        {
            try
            {
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n";
                System.Diagnostics.Debug.WriteLine(text);

                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Docked AI",
                    "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "startup.log"), text + Environment.NewLine);
            }
            catch
            {
                // Suppress logging failures to avoid recursive startup crashes.
            }
        }
    }
}


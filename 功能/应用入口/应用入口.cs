using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Docked_AI.Features.AppEntry;
using Docked_AI.Features.Tray;
using Docked_AI.Features.AppEntry.NormalLaunch;
using Docked_AI.Features.AppEntry.AutoLaunch;
using Docked_AI.Features.AppEntry.ShareLaunch;
using Docked_AI.Features.AppEntry.SingleInstance;
using Docked_AI.Features.MainWindow.Entry;
using Windows.Graphics;
using Windows.ApplicationModel.Activation;

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
        private SingleInstanceCommunication? _singleInstanceCommunication;
        
        // Launch handlers
        private NormalLaunchHandler? _normalLaunchHandler;
        private AutoLaunchHandler? _autoLaunchHandler;
        private ShareLaunchHandler? _shareLaunchHandler;

        // 单实例 Mutex（在构造函数中提前检测）
        private static Mutex? _singleInstanceMutex;
        private static bool _isMainInstance;

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
            // ⭐ 方案一：使用 Mutex 提前检测单实例，避免不必要的初始化
            // 这是最早的检测点，在 InitializeComponent() 之前执行
            _singleInstanceMutex = new Mutex(true, "DockedAI_SingleInstance_Mutex", out _isMainInstance);
            
            if (!_isMainInstance)
            {
                // 已有实例在运行 → 通知主实例显示窗口
                System.Diagnostics.Debug.WriteLine("[App] Another instance is already running, notifying main instance");
                SingleInstanceCommunication.NotifyShowWindow();
                
                // 立即退出，避免任何初始化
                System.Diagnostics.Debug.WriteLine("[App] Exiting secondary instance immediately");
                Environment.Exit(0);
                return;
            }

            System.Diagnostics.Debug.WriteLine("[App] This is the main instance, proceeding with initialization");
            
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
                // ⭐ 方案一：Mutex 已在构造函数中完成单实例检测
                // 如果代码执行到这里，说明当前是主实例
                System.Diagnostics.Debug.WriteLine("[App] Main instance confirmed, initializing application");

                // 启动单实例通信监听器（监听其他实例的唤醒请求）
                _singleInstanceCommunication = new SingleInstanceCommunication(OnShowWindowRequested);
                _singleInstanceCommunication.StartListening();
                System.Diagnostics.Debug.WriteLine("[App] Single instance communication listener started");

                // Initialize handlers
                _normalLaunchHandler = new NormalLaunchHandler(this);
                _autoLaunchHandler = new AutoLaunchHandler(this);
                _shareLaunchHandler = new ShareLaunchHandler(this);
                System.Diagnostics.Debug.WriteLine("[App] Launch handlers initialized");

                // Check for ShareTarget activation
                var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                System.Diagnostics.Debug.WriteLine($"[App] Activation kind: {activationArgs?.Kind}");

                // ShareTarget activation should always proceed
                if (activationArgs?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.ShareTarget)
                {
                    System.Diagnostics.Debug.WriteLine("[App] Handling ShareTarget activation");
                    HandleShareTargetActivation(activationArgs.Data as ShareTargetActivatedEventArgs);
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
                // - 使用 Mutex 在构造函数中提前检测单实例（最早检测点）
                // - 使用 EventWaitHandle 实现进程间通信（响应速度 < 5ms）
                // - 自启动时：不显示窗口，只在托盘运行
                // - 图标启动时：自动显示主窗口，提供更好的用户体验
                // - 再次点击图标时：新进程立即退出，通知主实例唤醒窗口
                // - 避免了 AppInstance 的进程初始化开销
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
        /// 处理显示窗口请求（当其他实例通知主实例时触发）
        /// </summary>
        private void OnShowWindowRequested()
        {
            System.Diagnostics.Debug.WriteLine("[App] OnShowWindowRequested called from another instance");
            
            // 显示主窗口
            _trayIconManager?.ShowMainWindow();
            
            // ⭐ 强制唤醒窗口到最前（Win32 API）
            BringWindowToFront(_window);
        }

        /// <summary>
        /// 强制唤醒窗口到最前（Win32 API）
        /// 解决点击图标后窗口在后台不弹出的问题
        /// </summary>
        private void BringWindowToFront(Window? window)
        {
            if (window == null)
            {
                return;
            }

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            // SW_SHOW = 5: 激活窗口并显示
            AppEntryWin32Api.ShowWindow(hwnd, 5);
            AppEntryWin32Api.SetForegroundWindow(hwnd);
            
            System.Diagnostics.Debug.WriteLine($"[App] BringWindowToFront: hwnd={hwnd}");
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
            
            // 注意：Activate() 的行为特性：
            // - 这是首次创建窗口时唯一合法的显示方案
            // - 会触发系统内置的流畅窗口显示动画（DWM 动画）
            // - 内置了强制进入可显示区域的逻辑
            // - 必须在所有窗口配置（位置、大小、样式等）完成后最后调用
            // - 如果在配置过程中调用会导致闪现问题
            _window.Activate();
        }

        private void OnAppExit(object sender, object e)
        {
            _trayIconManager?.Dispose();
            _singleInstanceCommunication?.Dispose();
        }

        private void ExitApplication()
        {
            try
            {
                _trayIconManager?.Dispose();
                _trayIconManager = null;
                
                _singleInstanceCommunication?.Dispose();
                _singleInstanceCommunication = null;
                
                // 释放 Mutex
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
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

            // 注意：Activate() 的行为特性：
            // - 这是首次创建窗口时唯一合法的显示方案
            // - 会触发系统内置的流畅窗口显示动画（DWM 动画）
            // - 内置了强制进入可显示区域的逻辑
            // - 必须在所有窗口配置（位置、大小、样式等）完成后最后调用
            // - 如果在配置过程中调用会导致闪现问题
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


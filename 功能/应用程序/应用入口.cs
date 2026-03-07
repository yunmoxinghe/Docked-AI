using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Docked_AI.Features.Tray;
using Windows.Graphics;

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
        private static Mutex? _mutex;
        private static bool _ownsMutex;

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
            try
            {
                const string appName = "DockedAI_SingleInstance_Mutex";
                _mutex = new Mutex(true, appName, out bool createdNew);
                _ownsMutex = createdNew;

                if (!createdNew)
                {
                    // Another instance is already running, bring it to the foreground.
                    ActivateExistingInstance();
                    ExitApplication();
                    return;
                }

                // Initialize the tray icon manager.
                _trayIconManager = new TrayIconManager(null, ExitApplication);
                _trayIconManager.Initialize();
                EnsureKeepAliveWindow();

                // Don't show the main window initially.
                // The window will only be shown when the user clicks the tray icon.
            }
            catch (Exception ex)
            {
                LogException("OnLaunched", ex);
                throw;
            }
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
            if (_window == null || _window.Content == null)
            {
                _window = new MainWindow();
            }

            _window.Activate();
        }

        private void OnAppExit(object sender, object e)
        {
            _trayIconManager?.Dispose();
            ReleaseMutexIfOwned();
        }

        private void ActivateExistingInstance()
        {
            try
            {
                // Find all processes with the same executable name.
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    // Skip the current process.
                    if (process.Id != currentProcess.Id)
                    {
                        // Try to bring the other process's window to foreground.
                        if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(process.MainWindowHandle, SW_SHOWNORMAL);
                            SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogException("ActivateExistingInstance", ex);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_SHOWNORMAL = 1;
        private const int SW_HIDE = 0;

        private void ExitApplication()
        {
            try
            {
                _trayIconManager?.Dispose();
                _trayIconManager = null;
                ReleaseMutexIfOwned();
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
                ShowWindow(hwnd, SW_HIDE);
            }
        }

        private static void ReleaseMutexIfOwned()
        {
            if (_mutex == null)
            {
                return;
            }

            try
            {
                if (_ownsMutex)
                {
                    _mutex.ReleaseMutex();
                }
            }
            catch (ApplicationException ex)
            {
                LogException("ReleaseMutexIfOwned", ex);
            }
            finally
            {
                _mutex.Dispose();
                _mutex = null;
                _ownsMutex = false;
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


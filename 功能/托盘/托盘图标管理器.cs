using System;
using System.IO;
using DevWinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Docked_AI.Features.Localization;
using Docked_AI.Features.Hotkey;
using Docked_AI.Features.Pages.Settings;

namespace Docked_AI.Features.Tray
{
    public class TrayIconManager : IDisposable
    {
        private SystemTrayIcon? _trayIcon;
        private Window? _mainWindow;
        private readonly Action? _exitAction;
        private GlobalHotkeyManager? _hotkeyManager;
        private Window? _keepAliveWindow;

        public TrayIconManager(Window? initialMainWindow, Action? exitAction = null)
        {
            _mainWindow = initialMainWindow;
            _exitAction = exitAction;

            // 订阅快捷键设置变化事件
            SettingsPage.HotkeySettingsChanged += OnHotkeySettingsChanged;
        }

        public void Initialize()
        {
            uint iconId = 123;
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sparkles.ico");
            _trayIcon = new SystemTrayIcon(iconId, iconPath, "Docked AI");

            _trayIcon.LeftClick += TrayIcon_LeftClick;
            _trayIcon.RightClick += TrayIcon_RightClick;
            _trayIcon.IsVisible = true;

            // 初始化全局快捷键
            InitializeGlobalHotkey();
        }

        private void InitializeGlobalHotkey()
        {
            try
            {
                // 创建一个隐藏的窗口用于接收快捷键消息
                _keepAliveWindow = new Window
                {
                    Title = "Docked AI Hotkey Handler"
                };

                // 不显示窗口
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_keepAliveWindow);
                
                // 注册快捷键
                RegisterHotkey();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrayIconManager] Failed to initialize global hotkey: {ex}");
            }
        }

        private void RegisterHotkey()
        {
            try
            {
                var settings = new HotkeySettings();

                if (!settings.IsEnabled || _keepAliveWindow == null)
                {
                    _hotkeyManager?.UnregisterHotkey();
                    return;
                }

                _hotkeyManager ??= new GlobalHotkeyManager();

                bool success = _hotkeyManager.RegisterHotkey(
                    _keepAliveWindow,
                    settings.Key,
                    settings.Ctrl,
                    settings.Alt,
                    settings.Shift,
                    settings.Win,
                    OnGlobalHotkeyPressed
                );

                if (success)
                {
                    System.Diagnostics.Debug.WriteLine($"[TrayIconManager] Global hotkey registered: {settings.GetDisplayText()}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[TrayIconManager] Failed to register global hotkey");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TrayIconManager] Error registering hotkey: {ex}");
            }
        }

        private void OnHotkeySettingsChanged(object? sender, EventArgs e)
        {
            // 重新注册快捷键
            RegisterHotkey();
        }

        private void OnGlobalHotkeyPressed()
        {
            // 快捷键被按下，显示/隐藏主窗口
            ShowMainWindow();
        }

        private void TrayIcon_LeftClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            ShowMainWindow();
        }

        private void TrayIcon_RightClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            var flyout = new MenuFlyout();

            var openItem = new MenuFlyoutItem
            {
                Text = LocalizationHelper.GetString("TrayMenu_OpenWindow"),
                Icon = new SymbolIcon(Symbol.GoToStart)
            };
            openItem.Click += (s, e) => ShowMainWindow();
            flyout.Items.Add(openItem);

            flyout.Items.Add(new MenuFlyoutSeparator());

            var exitItem = new MenuFlyoutItem
            {
                Text = LocalizationHelper.GetString("TrayMenu_Exit"),
                Icon = new FontIcon { Glyph = "\uF3B1" }
            };
            exitItem.Click += (s, e) => ExitApplication();
            flyout.Items.Add(exitItem);

            args.Flyout = flyout;
        }

        public void ShowMainWindow()
        {
            try
            {
                if (_mainWindow != null)
                {
                    var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                    if (windowHandle == IntPtr.Zero)
                    {
                        _mainWindow = null;
                    }
                }

                if (_mainWindow == null || _mainWindow.Content == null)
                {
                    _mainWindow = new global::Docked_AI.MainWindow();
                    _mainWindow.Activate();
                    WindowHelper.SetForegroundWindow(_mainWindow);
                }
                else
                {
                    if (_mainWindow is global::Docked_AI.MainWindow mainWindow)
                    {
                        mainWindow.ToggleWindow();
                        if (mainWindow.IsWindowVisible)
                        {
                            WindowHelper.SetForegroundWindow(_mainWindow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing main window: {ex.Message}");
                _mainWindow = null;
                _mainWindow = new global::Docked_AI.MainWindow();
                _mainWindow.Activate();
                WindowHelper.SetForegroundWindow(_mainWindow);
            }
        }

        public void ExitApplication()
        {
            SettingsPage.HotkeySettingsChanged -= OnHotkeySettingsChanged;

            if (_trayIcon != null)
            {
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                _trayIcon.RightClick -= TrayIcon_RightClick;
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _hotkeyManager?.Dispose();
            _hotkeyManager = null;

            _exitAction?.Invoke();
        }

        public void Dispose()
        {
            SettingsPage.HotkeySettingsChanged -= OnHotkeySettingsChanged;

            if (_trayIcon != null)
            {
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                _trayIcon.RightClick -= TrayIcon_RightClick;
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _hotkeyManager?.Dispose();
            _hotkeyManager = null;
        }
    }
}

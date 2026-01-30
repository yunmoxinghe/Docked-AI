using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using DevWinUI;

namespace Docked_AI
{
    public class TrayIconManager : IDisposable
    {
        private SystemTrayIcon? _trayIcon;
        private Window? _mainWindow;
        private readonly Action? _exitAction;

        public TrayIconManager(Window? initialMainWindow, Action? exitAction = null)
        {
            _mainWindow = initialMainWindow;
            _exitAction = exitAction;
        }

        public void Initialize()
        {
            uint iconId = 123;
            
            // 使用项目图标初始化托盘图标
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sparkles.ico");
            _trayIcon = new SystemTrayIcon(iconId, iconPath, "Docked AI");
            
            _trayIcon.LeftClick += TrayIcon_LeftClick;
            _trayIcon.RightClick += TrayIcon_RightClick;
            _trayIcon.IsVisible = true;
        }

        private void TrayIcon_LeftClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            ShowMainWindow();
        }

        private void TrayIcon_RightClick(SystemTrayIcon sender, SystemTrayIconEventArgs args)
        {
            var flyout = new MenuFlyout();
            
            var openItem = new MenuFlyoutItem() 
            { 
                Text = "打开主窗口",
                Icon = new SymbolIcon(Symbol.GoToStart)
            };
            openItem.Click += (s, e) =>
            {
                ShowMainWindow();
            };
            flyout.Items.Add(openItem);
            
            flyout.Items.Add(new MenuFlyoutSeparator());
            
            var exitItem = new MenuFlyoutItem() 
            { 
                Text = "退出",
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
                // 检查现有窗口是否已关闭
                if (_mainWindow != null)
                {
                    var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                    if (windowHandle == IntPtr.Zero)
                    {
                        // 窗口已关闭，需要重新创建
                        _mainWindow = null;
                    }
                }

                if (_mainWindow == null || _mainWindow.Content == null)
                {
                    _mainWindow = new MainWindow();
                }
                
                _mainWindow.Activate();
                WindowHelper.SetForegroundWindow(_mainWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing main window: {ex.Message}");
                // 发生错误时重新创建窗口
                _mainWindow = null;
                _mainWindow = new MainWindow();
                _mainWindow.Activate();
                WindowHelper.SetForegroundWindow(_mainWindow);
            }
        }

        public void ExitApplication()
        {
            if (_trayIcon != null)
            {
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                _trayIcon.RightClick -= TrayIcon_RightClick;
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _exitAction?.Invoke();
        }

        public void Dispose()
        {
            if (_trayIcon != null)
            {
                _trayIcon.LeftClick -= TrayIcon_LeftClick;
                _trayIcon.RightClick -= TrayIcon_RightClick;
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
    }
}
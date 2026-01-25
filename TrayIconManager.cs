using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.UI.Xaml;

// Alias the two Window types to avoid conflicts
using WinUIWindow = Microsoft.UI.Xaml.Window;
using WPFWindow = System.Windows.Window;

namespace Docked_AI
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private WinUIWindow? _mainWindow;
        private readonly Action? _exitAction;

        public TrayIconManager(WinUIWindow? initialMainWindow, Action? exitAction = null)
        {
            _mainWindow = initialMainWindow;
            _exitAction = exitAction;
        }

        public void Initialize()
        {
            // Create the notify icon
            _notifyIcon = new NotifyIcon();
            
            // Load icon from resources
            try
            {
                // Try to load from embedded resource or use a default icon
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application;
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application; // fallback to default icon
            }
            
            _notifyIcon.Text = "Docked AI";
            _notifyIcon.Visible = true;
            
            // Create context menu
            var contextMenu = new ContextMenuStrip();
            
            var openItem = new ToolStripMenuItem("打开主窗口");
            openItem.Click += (sender, e) => ShowMainWindow();
            
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (sender, e) => ExitApplication();
            
            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
            
            // Handle double-click to show main window
            _notifyIcon.DoubleClick += (sender, e) => ShowMainWindow();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null || _mainWindow.Content == null)
            {
                _mainWindow = new MainWindow();
            }
            
            _mainWindow.Activate();
        }

        public void ExitApplication()
        {
            _exitAction?.Invoke();
            _notifyIcon?.Dispose();
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }
    }
}
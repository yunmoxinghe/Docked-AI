using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Appearance
{
    internal sealed class TitleBarService
    {
        public void ConfigureTitleBarAndBorder(Window window)
        {
            try
            {
                IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                    presenter.IsResizable = true;
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = false;
                }

                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure title bar: {ex.Message}");
            }
        }
    }
}

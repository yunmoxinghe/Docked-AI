using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Appearance
{
    internal sealed class TitleBarService
    {
        public void ConfigureStandardWindow(Window window)
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
                appWindow.TitleBar.ButtonForegroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Transparent;

                int cornerPreference = AppearanceWin32Api.DWMWCP_DEFAULT;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    sizeof(int));

                int borderColor = AppearanceWin32Api.DWMWA_COLOR_DEFAULT;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_BORDER_COLOR,
                    ref borderColor,
                    sizeof(int));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure title bar: {ex.Message}");
            }
        }

        public void ConfigurePinnedWindow(Window window)
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
                    presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                    presenter.IsResizable = true;
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                }

                // 不要扩展内容到标题栏区域，因为我们要完全移除标题栏
                appWindow.TitleBar.ExtendsContentIntoTitleBar = false;

                int cornerPreference = AppearanceWin32Api.DWMWCP_DONOTROUND;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    sizeof(int));

                int borderColor = AppearanceWin32Api.DWMWA_COLOR_NONE;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_BORDER_COLOR,
                    ref borderColor,
                    sizeof(int));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to configure pinned window: {ex.Message}");
            }
        }
    }
}

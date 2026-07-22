using System;
using Microsoft.UI.Xaml;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 窗口辅助类
    /// 提供窗口焦点和前台设置的辅助方法
    /// </summary>
    internal static class WindowHelper
    {
        /// <summary>
        /// 设置窗口为前台窗口并获取焦点
        /// 使用多种技术确保窗口能够成功获得焦点
        /// </summary>
        /// <param name="window">要设置为前台的窗口</param>
        public static void SetForegroundWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                // 方法1: 先显示窗口
                TrayWin32Api.ShowWindow(hwnd, TrayWin32Api.SW_SHOW);

                // 方法2: 使用线程输入附加技术
                // 这是解决 SetForegroundWindow 限制的关键技术
                var foregroundWindow = TrayWin32Api.GetForegroundWindow();
                if (foregroundWindow != hwnd)
                {
                    var currentThreadId = TrayWin32Api.GetCurrentThreadId();
                    TrayWin32Api.GetWindowThreadProcessId(foregroundWindow, out uint foregroundThreadId);

                    // 附加到前台窗口的线程
                    if (foregroundThreadId != currentThreadId)
                    {
                        TrayWin32Api.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                        TrayWin32Api.BringWindowToTop(hwnd);
                        TrayWin32Api.SetForegroundWindow(hwnd);
                        TrayWin32Api.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                    else
                    {
                        TrayWin32Api.BringWindowToTop(hwnd);
                        TrayWin32Api.SetForegroundWindow(hwnd);
                    }
                }

                // 方法3: 短暂设置为 TOPMOST 然后取消
                // 这可以帮助窗口获得 Z-order 优先级
                TrayWin32Api.SetWindowPos(hwnd, TrayWin32Api.HWND_TOPMOST, 0, 0, 0, 0, TrayWin32Api.SWP_NOMOVE | TrayWin32Api.SWP_NOSIZE | TrayWin32Api.SWP_SHOWWINDOW);
                TrayWin32Api.SetWindowPos(hwnd, TrayWin32Api.HWND_NOTOPMOST, 0, 0, 0, 0, TrayWin32Api.SWP_NOMOVE | TrayWin32Api.SWP_NOSIZE | TrayWin32Api.SWP_SHOWWINDOW);

                // 方法4: 最后再次调用 SetForegroundWindow
                TrayWin32Api.SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHelper] Failed to set foreground window: {ex.Message}");
            }
        }
    }
}

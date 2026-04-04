using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Docked_AI.Features.Tray
{
    /// <summary>
    /// 窗口辅助类
    /// 提供窗口焦点和前台设置的辅助方法
    /// </summary>
    internal static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

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
                ShowWindow(hwnd, SW_SHOW);

                // 方法2: 使用线程输入附加技术
                // 这是解决 SetForegroundWindow 限制的关键技术
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != hwnd)
                {
                    var currentThreadId = GetCurrentThreadId();
                    GetWindowThreadProcessId(foregroundWindow, out uint foregroundThreadId);

                    // 附加到前台窗口的线程
                    if (foregroundThreadId != currentThreadId)
                    {
                        AttachThreadInput(currentThreadId, foregroundThreadId, true);
                        BringWindowToTop(hwnd);
                        SetForegroundWindow(hwnd);
                        AttachThreadInput(currentThreadId, foregroundThreadId, false);
                    }
                    else
                    {
                        BringWindowToTop(hwnd);
                        SetForegroundWindow(hwnd);
                    }
                }

                // 方法3: 短暂设置为 TOPMOST 然后取消
                // 这可以帮助窗口获得 Z-order 优先级
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                // 方法4: 最后再次调用 SetForegroundWindow
                SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHelper] Failed to set foreground window: {ex.Message}");
            }
        }
    }
}

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.Pages.Lab
{
    /// <summary>
    /// 窗口句柄获取辅助类
    /// 解决 WinUI 3 在托盘架构下从 Page 获取 HWND 的问题
    /// </summary>
    public static class WindowHandleHelper
    {
        /// <summary>
        /// 从 Page 获取其所在窗口的句柄
        /// </summary>
        /// <param name="page">页面实例</param>
        /// <returns>窗口句柄，失败返回 IntPtr.Zero</returns>
        public static IntPtr GetWindowHandleFromPage(Page page)
        {
            if (page == null)
            {
                System.Diagnostics.Debug.WriteLine("[WindowHandleHelper] Page is null");
                return IntPtr.Zero;
            }

            // 方法 1: 尝试从 XamlRoot 获取窗口
            if (page.XamlRoot?.Content is FrameworkElement rootElement)
            {
                try
                {
                    // 遍历可视树找到 Window
                    DependencyObject? current = rootElement;
                    while (current != null)
                    {
                        // 检查父元素是否是 Window
                        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
                        
                        if (parent != null)
                        {
                            // 尝试转换为 Window
                            var windowType = parent.GetType();
                            if (windowType.Name == "Window" || windowType.BaseType?.Name == "Window")
                            {
                                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(parent);
                                System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ✅ 从可视树找到 Window: {hwnd}");
                                return hwnd;
                            }
                        }

                        current = parent;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ❌ 可视树遍历失败: {ex.Message}");
                }
            }

            // 方法 2: 尝试从 App 获取活动窗口
            try
            {
                var app = Application.Current as App;
                if (app?.MainWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(app.MainWindow);
                    System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ✅ 从 App.MainWindow 获取: {hwnd}");
                    return hwnd;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WindowHandleHelper] ⚠️ App.MainWindow 为 null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ❌ 从 App.MainWindow 获取失败: {ex.Message}");
            }

            // 方法 3: 使用 Win32 API 查找当前进程的窗口
            try
            {
                var hwnd = GetCurrentProcessMainWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ✅ 从进程窗口列表获取: {hwnd}");
                    return hwnd;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] ❌ Win32 API 查找失败: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("[WindowHandleHelper] ❌ 所有方法都失败了");
            return IntPtr.Zero;
        }

        /// <summary>
        /// 获取当前进程的主窗口句柄（通过 Win32 API）
        /// </summary>
        private static IntPtr GetCurrentProcessMainWindowHandle()
        {
            var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr foundHwnd = IntPtr.Zero;

            // 枚举所有顶级窗口
            Win32.EnumWindows((hwnd, lParam) =>
            {
                // 获取窗口所属进程 ID
                Win32.GetWindowThreadProcessId(hwnd, out uint processId);
                
                if (processId == currentProcessId)
                {
                    // 检查窗口是否可见且不是工具窗口
                    if (Win32.IsWindowVisible(hwnd))
                    {
                        // 获取窗口标题
                        var titleLength = Win32.GetWindowTextLength(hwnd);
                        if (titleLength > 0)
                        {
                            var title = new System.Text.StringBuilder(titleLength + 1);
                            Win32.GetWindowText(hwnd, title, title.Capacity);
                            
                            // 排除 keep-alive 窗口（通常没有标题或标题为空）
                            // 优先选择有标题的窗口
                            if (!string.IsNullOrEmpty(title.ToString()))
                            {
                                foundHwnd = hwnd;
                                System.Diagnostics.Debug.WriteLine($"[WindowHandleHelper] 找到窗口: {title}, HWND: {hwnd}");
                                return false; // 停止枚举
                            }
                        }
                    }
                }
                
                return true; // 继续枚举
            }, IntPtr.Zero);

            return foundHwnd;
        }

        /// <summary>
        /// Win32 API 声明
        /// </summary>
        private static class Win32
        {
            public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowTextLength(IntPtr hWnd);
        }
    }
}

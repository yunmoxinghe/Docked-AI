using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Entry
{
    /// <summary>
    /// 主窗口工厂类，负责统一创建和管理主窗口实例
    /// </summary>
    public static class MainWindowFactory
    {
        /// <summary>
        /// 创建并激活主窗口
        /// </summary>
        /// <returns>新创建的主窗口实例</returns>
        public static global::Docked_AI.MainWindow CreateAndActivate()
        {
            var window = new global::Docked_AI.MainWindow();
            window.Activate();
            return window;
        }

        /// <summary>
        /// 创建主窗口但不激活
        /// </summary>
        /// <returns>新创建的主窗口实例</returns>
        public static global::Docked_AI.MainWindow Create()
        {
            return new global::Docked_AI.MainWindow();
        }

        /// <summary>
        /// 获取或创建主窗口
        /// 如果现有窗口无效，则创建新窗口
        /// </summary>
        /// <param name="existingWindow">现有窗口引用</param>
        /// <returns>有效的主窗口实例</returns>
        public static global::Docked_AI.MainWindow GetOrCreate(Window? existingWindow)
        {
            if (IsWindowValid(existingWindow))
            {
                return (global::Docked_AI.MainWindow)existingWindow!;
            }

            return Create();
        }

        /// <summary>
        /// 检查窗口是否有效
        /// </summary>
        /// <param name="window">要检查的窗口</param>
        /// <returns>窗口是否有效</returns>
        public static bool IsWindowValid(Window? window)
        {
            if (window == null)
            {
                return false;
            }

            try
            {
                // 检查窗口句柄是否有效
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (windowHandle == IntPtr.Zero)
                {
                    return false;
                }

                // 检查窗口内容是否存在
                return window.Content != null;
            }
            catch
            {
                return false;
            }
        }
    }
}

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace Docked_AI.Features.MainWindow.Appearance
{
    /// <summary>
    /// 标题栏服务 - 配置窗口标题栏样式
    /// 
    /// 【文件职责】
    /// 1. 配置标准窗口的标题栏（有边框、可调整大小）
    /// 2. 配置固定窗口的标题栏（无边框、不可调整大小）
    /// 3. 设置 DWM 属性（圆角、边框颜色）
    /// 
    /// 【核心设计】
    /// 
    /// 为什么需要两种配置？
    /// - 标准窗口：浮动窗口，需要边框和调整大小功能
    /// - 固定窗口：AppBar 模式，贴边显示，不需要边框
    /// 
    /// 为什么使用 ExtendsContentIntoTitleBar？
    /// - 标准窗口：扩展内容到标题栏区域，实现自定义标题栏
    /// - 固定窗口：不扩展，完全移除标题栏
    /// 
    /// 【核心逻辑流程】
    /// 
    /// 配置标准窗口流程：
    ///   1. 获取窗口句柄和 AppWindow
    ///   2. 配置 OverlappedPresenter：
    ///      - 有边框、无标题栏
    ///      - 可调整大小、可最大化、不可最小化
    ///      - 始终置顶
    ///   3. 配置 TitleBar：
    ///      - 扩展内容到标题栏区域
    ///      - 折叠标题栏高度
    ///      - 按钮背景透明
    ///   4. 设置 DWM 属性：
    ///      - 默认圆角（DWMWCP_DEFAULT）
    ///      - 默认边框颜色（DWMWA_COLOR_DEFAULT）
    /// 
    /// 配置固定窗口流程：
    ///   1. 获取窗口句柄和 AppWindow
    ///   2. 配置 OverlappedPresenter：
    ///      - 无边框、无标题栏
    ///      - 可调整大小（但实际由 AppBar 控制）
    ///      - 不可最大化、不可最小化
    ///      - 始终置顶
    ///   3. 配置 TitleBar：
    ///      - 不扩展内容到标题栏区域
    ///   4. 设置 DWM 属性：
    ///      - 不圆角（DWMWCP_DONOTROUND）
    ///      - 无边框颜色（DWMWA_COLOR_NONE）
    /// 
    /// 【关键依赖关系】
    /// - Window: WinUI 窗口对象
    /// - AppWindow: WinUI 窗口管理对象
    /// - OverlappedPresenter: 窗口呈现器，控制边框、标题栏、调整大小
    /// - AppearanceWin32Api: Win32 API 封装，提供 DwmSetWindowAttribute
    /// 
    /// 【潜在副作用】
    /// 1. 修改窗口呈现器属性（边框、标题栏、调整大小）
    /// 2. 修改标题栏属性（扩展内容、高度、按钮颜色）
    /// 3. 修改 DWM 属性（圆角、边框颜色）
    /// 
    /// 【重构风险点】
    /// 1. ExtendsContentIntoTitleBar 的设置：
    ///    - 标准窗口必须设置为 true，否则无法自定义标题栏
    ///    - 固定窗口必须设置为 false，否则标题栏区域仍然存在
    /// 2. IsResizable 的设置：
    ///    - 标准窗口设置为 true，允许用户调整大小
    ///    - 固定窗口设置为 true，但实际由 AppBar 控制（不允许用户调整）
    ///    - 如果固定窗口设置为 false，可能导致 AppBar 边界计算错误
    /// 3. IsAlwaysOnTop 的设置：
    ///    - 两种模式都设置为 true，确保窗口始终在最前面
    ///    - 如果设置为 false，窗口可能被其他窗口遮挡
    /// 4. DWM 属性的设置：
    ///    - 标准窗口使用默认圆角和边框颜色
    ///    - 固定窗口使用不圆角和无边框颜色
    ///    - 如果设置错误，窗口外观不符合预期
    /// 5. 异常处理：
    ///    - 所有异常都被捕获并记录到调试输出
    ///    - 如果配置失败，窗口可能使用默认样式
    /// </summary>
    internal sealed class TitleBarService
    {
        /// <summary>
        /// 配置标准窗口 - 浮动窗口模式
        /// 
        /// 【调用时机】
        /// - 窗口初始化时
        /// - 从固定模式切换到标准模式时
        /// 
        /// 【配置特点】
        /// - 有边框、无标题栏（自定义标题栏）
        /// - 可调整大小、可最大化、不可最小化
        /// - 始终置顶
        /// - 扩展内容到标题栏区域
        /// - 默认圆角和边框颜色
        /// 
        /// 【副作用】
        /// 修改窗口呈现器、标题栏、DWM 属性
        /// </summary>
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

                // 配置窗口呈现器
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                    presenter.IsResizable = true;
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsMaximizable = true;
                    presenter.IsMinimizable = false;
                }

                // 配置标题栏
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonForegroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Transparent;

                // 设置 DWM 属性：默认圆角
                int cornerPreference = AppearanceWin32Api.DWMWCP_DEFAULT;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    sizeof(int));

                // 设置 DWM 属性：默认边框颜色
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

        /// <summary>
        /// 配置固定窗口 - AppBar 模式
        /// 
        /// 【调用时机】
        /// 从标准模式切换到固定模式时
        /// 
        /// 【配置特点】
        /// - 无边框、无标题栏
        /// - 可调整大小（但实际由 AppBar 控制）
        /// - 不可最大化、不可最小化
        /// - 始终置顶
        /// - 不扩展内容到标题栏区域
        /// - 不圆角、无边框颜色
        /// 
        /// 【副作用】
        /// 修改窗口呈现器、标题栏、DWM 属性
        /// 
        /// 【设计原因】
        /// 为什么 IsResizable 设置为 true？
        /// - AppBar 需要调整窗口大小以适应屏幕边缘
        /// - 但用户不能手动调整大小（由 AppBar 控制）
        /// </summary>
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

                // 配置窗口呈现器
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                    presenter.IsResizable = true;  // AppBar 需要调整大小
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                }

                // 不要扩展内容到标题栏区域，因为我们要完全移除标题栏
                appWindow.TitleBar.ExtendsContentIntoTitleBar = false;

                // 设置 DWM 属性：不圆角
                int cornerPreference = AppearanceWin32Api.DWMWCP_DONOTROUND;
                _ = AppearanceWin32Api.DwmSetWindowAttribute(
                    hWnd,
                    AppearanceWin32Api.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPreference,
                    sizeof(int));

                // 设置 DWM 属性：无边框颜色
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

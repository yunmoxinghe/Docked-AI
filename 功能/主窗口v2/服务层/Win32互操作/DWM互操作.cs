using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// DWM (Desktop Window Manager) API 互操作
/// 提供 DWM 相关的 P/Invoke 声明，用于设置窗口的视觉效果（圆角、亚克力背景等）
/// </summary>
public static class DWM互操作
{
    /// <summary>
    /// 设置 DWM 窗口属性
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="dwAttribute">属性类型</param>
    /// <param name="pvAttribute">属性值指针</param>
    /// <param name="cbAttribute">属性值大小</param>
    /// <returns>成功返回 0，失败返回错误代码</returns>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DWMWINDOWATTRIBUTE dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    /// <summary>
    /// 扩展窗口框架到客户区
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="pMarInset">边距</param>
    /// <returns>成功返回 S_OK (0)，失败返回错误代码</returns>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmExtendFrameIntoClientArea(
        IntPtr hwnd,
        ref MARGINS pMarInset);

    /// <summary>
    /// DWM 窗口属性枚举
    /// </summary>
    public enum DWMWINDOWATTRIBUTE
    {
        /// <summary>
        /// 窗口圆角偏好
        /// </summary>
        DWMWA_WINDOW_CORNER_PREFERENCE = 33,

        /// <summary>
        /// 系统背景类型（亚克力效果）
        /// </summary>
        DWMWA_SYSTEMBACKDROP_TYPE = 38,

        /// <summary>
        /// 标题栏颜色
        /// </summary>
        DWMWA_CAPTION_COLOR = 35,

        /// <summary>
        /// 边框颜色
        /// </summary>
        DWMWA_BORDER_COLOR = 34,

        /// <summary>
        /// 使用暗色模式
        /// </summary>
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    }

    /// <summary>
    /// 窗口圆角偏好
    /// </summary>
    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        /// <summary>
        /// 默认圆角（由系统决定）
        /// </summary>
        DWMWCP_DEFAULT = 0,

        /// <summary>
        /// 不使用圆角
        /// </summary>
        DWMWCP_DONOTROUND = 1,

        /// <summary>
        /// 使用圆角
        /// </summary>
        DWMWCP_ROUND = 2,

        /// <summary>
        /// 使用小圆角
        /// </summary>
        DWMWCP_ROUNDSMALL = 3
    }

    /// <summary>
    /// 系统背景类型（亚克力效果）
    /// </summary>
    public enum DWM_SYSTEMBACKDROP_TYPE
    {
        /// <summary>
        /// 自动选择
        /// </summary>
        DWMSBT_AUTO = 0,

        /// <summary>
        /// 无背景效果
        /// </summary>
        DWMSBT_NONE = 1,

        /// <summary>
        /// 主窗口背景（Mica）
        /// </summary>
        DWMSBT_MAINWINDOW = 2,

        /// <summary>
        /// 瞬态窗口背景（亚克力）
        /// </summary>
        DWMSBT_TRANSIENTWINDOW = 3,

        /// <summary>
        /// 标签窗口背景（Mica Alt）
        /// </summary>
        DWMSBT_TABBEDWINDOW = 4
    }

    /// <summary>
    /// 边距结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        public MARGINS(int left, int right, int top, int bottom)
        {
            cxLeftWidth = left;
            cxRightWidth = right;
            cyTopHeight = top;
            cyBottomHeight = bottom;
        }
    }
}

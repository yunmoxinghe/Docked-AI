using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// AppBar 相关的 Win32 API 封装
/// 用于将窗口注册为 AppBar（类似任务栏），占用屏幕工作区
/// </summary>
internal static class AppBar互操作
{
    /// <summary>
    /// 与 Shell 交互 AppBar
    /// 告诉系统"我这个窗口要像任务栏那样占据桌面边缘的一块区域"
    /// </summary>
    [DllImport("shell32.dll")]
    internal static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    /// <summary>
    /// 向系统注册一条"本应用专用消息"
    /// 这样 AppBar 之类的回调消息就不会跟别的消息号撞车
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    /// <summary>
    /// AppBar 数据结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;

        /// <summary>
        /// 创建默认的 APPBARDATA 实例
        /// </summary>
        public static APPBARDATA Create(IntPtr hwnd, uint callbackMessage, AppBarEdge edge)
        {
            return new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = callbackMessage,
                uEdge = (uint)edge,
                rc = new RECT(),
                lParam = IntPtr.Zero
            };
        }
    }

    /// <summary>
    /// 矩形结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    // AppBar 消息常量
    /// <summary>
    /// 注册一个新的 AppBar
    /// </summary>
    internal const uint ABM_NEW = 0x00000000;

    /// <summary>
    /// 移除 AppBar
    /// </summary>
    internal const uint ABM_REMOVE = 0x00000001;

    /// <summary>
    /// 查询系统"这个位置能不能占、该怎么修正"
    /// </summary>
    internal const uint ABM_QUERYPOS = 0x00000002;

    /// <summary>
    /// 正式把修正后的位置提交给系统
    /// </summary>
    internal const uint ABM_SETPOS = 0x00000003;
}

/// <summary>
/// AppBar 边缘位置枚举
/// </summary>
public enum AppBarEdge
{
    /// <summary>
    /// 停靠在屏幕左边
    /// </summary>
    Left = 0,

    /// <summary>
    /// 停靠在屏幕顶部
    /// </summary>
    Top = 1,

    /// <summary>
    /// 停靠在屏幕右边
    /// </summary>
    Right = 2,

    /// <summary>
    /// 停靠在屏幕底部
    /// </summary>
    Bottom = 3
}

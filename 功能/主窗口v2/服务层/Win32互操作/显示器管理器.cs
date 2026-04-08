using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// 显示器管理相关的 Win32 API 互操作
/// 提供多显示器支持、显示器信息查询等功能的 P/Invoke 声明
/// </summary>
public static class 显示器管理器
{
    /// <summary>
    /// 枚举显示器的回调函数委托
    /// </summary>
    /// <param name="hMonitor">显示器句柄</param>
    /// <param name="hdcMonitor">显示器设备上下文</param>
    /// <param name="lprcMonitor">显示器矩形</param>
    /// <param name="dwData">用户数据</param>
    /// <returns>返回 true 继续枚举，返回 false 停止枚举</returns>
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    /// <summary>
    /// 枚举所有显示器
    /// </summary>
    /// <param name="lpfnEnum">枚举回调函数</param>
    /// <param name="dwData">用户数据</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    /// <summary>
    /// 获取显示器信息
    /// </summary>
    /// <param name="hMonitor">显示器句柄</param>
    /// <param name="lpmi">接收显示器信息的结构</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// 从窗口获取显示器句柄
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="dwFlags">标志</param>
    /// <returns>显示器句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    /// <summary>
    /// 从点获取显示器句柄
    /// </summary>
    /// <param name="pt">点坐标</param>
    /// <param name="dwFlags">标志</param>
    /// <returns>显示器句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>
    /// 从矩形获取显示器句柄
    /// </summary>
    /// <param name="lprc">矩形</param>
    /// <param name="dwFlags">标志</param>
    /// <returns>显示器句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    // ===== 常量定义 =====

    /// <summary>
    /// MonitorFrom* 函数的标志
    /// </summary>
    public const uint MONITOR_DEFAULTTONULL = 0x00000000;  // 返回 NULL
    public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;  // 返回主显示器
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;  // 返回最近的显示器

    /// <summary>
    /// 显示器信息标志
    /// </summary>
    public const uint MONITORINFOF_PRIMARY = 0x00000001;  // 主显示器

    // ===== 结构定义 =====

    /// <summary>
    /// 显示器信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;  // 显示器矩形（虚拟屏幕坐标）
        public RECT rcWork;     // 工作区矩形（不包括任务栏）
        public uint dwFlags;    // 标志（MONITORINFOF_PRIMARY）

        public static MONITORINFO Default
        {
            get
            {
                var result = new MONITORINFO();
                result.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                return result;
            }
        }
    }

    /// <summary>
    /// 扩展显示器信息结构（包含设备名称）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;

        public static MONITORINFOEX Default
        {
            get
            {
                var result = new MONITORINFOEX();
                result.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFOEX));
                return result;
            }
        }
    }

    /// <summary>
    /// 矩形结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    /// <summary>
    /// 点结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

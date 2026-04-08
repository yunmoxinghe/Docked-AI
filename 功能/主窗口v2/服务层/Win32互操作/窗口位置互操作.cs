using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// 窗口位置和尺寸相关的 Win32 API 互操作
/// 提供窗口移动、调整大小、Z轴顺序等功能的 P/Invoke 声明
/// </summary>
public static class 窗口位置互操作
{
    /// <summary>
    /// 设置窗口位置、大小和 Z 轴顺序
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="hWndInsertAfter">Z 轴顺序参考窗口</param>
    /// <param name="X">新的 X 坐标</param>
    /// <param name="Y">新的 Y 坐标</param>
    /// <param name="cx">新的宽度</param>
    /// <param name="cy">新的高度</param>
    /// <param name="uFlags">窗口定位标志</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    /// <summary>
    /// 获取窗口矩形（屏幕坐标）
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="lpRect">接收窗口矩形的结构</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 获取客户区矩形（客户区坐标）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 移动窗口
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="X">新的 X 坐标</param>
    /// <param name="Y">新的 Y 坐标</param>
    /// <param name="nWidth">新的宽度</param>
    /// <param name="nHeight">新的高度</param>
    /// <param name="bRepaint">是否重绘</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveWindow(
        IntPtr hWnd,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        bool bRepaint);

    /// <summary>
    /// 调整窗口矩形以适应指定的客户区大小
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustWindowRectEx(
        ref RECT lpRect,
        uint dwStyle,
        bool bMenu,
        uint dwExStyle);

    /// <summary>
    /// 获取系统度量值
    /// </summary>
    /// <param name="nIndex">度量值索引</param>
    /// <returns>度量值</returns>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    /// <summary>
    /// 获取或设置系统参数
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref RECT pvParam,
        uint fWinIni);

    // ===== 常量定义 =====

    /// <summary>
    /// SetWindowPos 的 hWndInsertAfter 参数特殊值
    /// </summary>
    public static readonly IntPtr HWND_TOP = new IntPtr(0);
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    /// <summary>
    /// SetWindowPos 标志
    /// </summary>
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const uint SWP_NOCOPYBITS = 0x0100;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_NOSENDCHANGING = 0x0400;
    public const uint SWP_DRAWFRAME = SWP_FRAMECHANGED;
    public const uint SWP_NOREPOSITION = SWP_NOOWNERZORDER;
    public const uint SWP_DEFERERASE = 0x2000;
    public const uint SWP_ASYNCWINDOWPOS = 0x4000;

    /// <summary>
    /// GetSystemMetrics 索引常量
    /// </summary>
    public const int SM_CXSCREEN = 0;  // 主显示器宽度
    public const int SM_CYSCREEN = 1;  // 主显示器高度
    public const int SM_CXVIRTUALSCREEN = 78;  // 虚拟屏幕宽度（所有显示器）
    public const int SM_CYVIRTUALSCREEN = 79;  // 虚拟屏幕高度（所有显示器）
    public const int SM_XVIRTUALSCREEN = 76;  // 虚拟屏幕左边界
    public const int SM_YVIRTUALSCREEN = 77;  // 虚拟屏幕上边界
    public const int SM_CMONITORS = 80;  // 显示器数量

    /// <summary>
    /// SystemParametersInfo 动作常量
    /// </summary>
    public const uint SPI_GETWORKAREA = 0x0030;  // 获取工作区（不包括任务栏）
    public const uint SPI_SETWORKAREA = 0x002F;  // 设置工作区

    /// <summary>
    /// 矩形结构（与窗口样式互操作中的 RECT 相同，为了方便使用在此重新定义）
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
}

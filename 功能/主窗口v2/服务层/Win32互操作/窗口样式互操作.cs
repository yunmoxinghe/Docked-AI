using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// 窗口样式相关的 Win32 API 互操作
/// 提供窗口样式、扩展样式、显示/隐藏等功能的 P/Invoke 声明
/// </summary>
public static class 窗口样式互操作
{
    /// <summary>
    /// 获取窗口长整型值（样式、扩展样式等）
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="nIndex">要获取的值的索引</param>
    /// <returns>窗口属性值</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 设置窗口长整型值（样式、扩展样式等）
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="nIndex">要设置的值的索引</param>
    /// <param name="dwNewLong">新的属性值</param>
    /// <returns>之前的属性值</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// 获取窗口长整型值（64位版本）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 设置窗口长整型值（64位版本）
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// 显示或隐藏窗口
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="nCmdShow">显示命令</param>
    /// <returns>如果窗口之前可见返回 true，否则返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// 设置窗口的显示状态（激活、最小化、最大化等）
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    /// <summary>
    /// 获取窗口的显示状态
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    // ===== 常量定义 =====

    /// <summary>
    /// GetWindowLong/SetWindowLong 的索引常量
    /// </summary>
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    /// <summary>
    /// 窗口样式常量
    /// </summary>
    public const int WS_OVERLAPPED = 0x00000000;
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_CHILD = 0x40000000;
    public const int WS_MINIMIZE = 0x20000000;
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_DISABLED = 0x08000000;
    public const int WS_CLIPSIBLINGS = 0x04000000;
    public const int WS_CLIPCHILDREN = 0x02000000;
    public const int WS_MAXIMIZE = 0x01000000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_BORDER = 0x00800000;
    public const int WS_DLGFRAME = 0x00400000;
    public const int WS_VSCROLL = 0x00200000;
    public const int WS_HSCROLL = 0x00100000;
    public const int WS_SYSMENU = 0x00080000;
    public const int WS_THICKFRAME = 0x00040000;
    public const int WS_GROUP = 0x00020000;
    public const int WS_TABSTOP = 0x00010000;
    public const int WS_MINIMIZEBOX = 0x00020000;
    public const int WS_MAXIMIZEBOX = 0x00010000;

    /// <summary>
    /// 组合样式
    /// </summary>
    public const int WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
    public const int WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU;

    /// <summary>
    /// 扩展窗口样式常量
    /// </summary>
    public const int WS_EX_DLGMODALFRAME = 0x00000001;
    public const int WS_EX_NOPARENTNOTIFY = 0x00000004;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_ACCEPTFILES = 0x00000010;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_MDICHILD = 0x00000040;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_WINDOWEDGE = 0x00000100;
    public const int WS_EX_CLIENTEDGE = 0x00000200;
    public const int WS_EX_CONTEXTHELP = 0x00000400;
    public const int WS_EX_RIGHT = 0x00001000;
    public const int WS_EX_LEFT = 0x00000000;
    public const int WS_EX_RTLREADING = 0x00002000;
    public const int WS_EX_LTRREADING = 0x00000000;
    public const int WS_EX_LEFTSCROLLBAR = 0x00004000;
    public const int WS_EX_RIGHTSCROLLBAR = 0x00000000;
    public const int WS_EX_CONTROLPARENT = 0x00010000;
    public const int WS_EX_STATICEDGE = 0x00020000;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOINHERITLAYOUT = 0x00100000;
    public const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    public const int WS_EX_LAYOUTRTL = 0x00400000;
    public const int WS_EX_COMPOSITED = 0x02000000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>
    /// ShowWindow 命令常量
    /// </summary>
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_NORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_MAXIMIZE = 3;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const int SW_SHOWNA = 8;
    public const int SW_RESTORE = 9;
    public const int SW_SHOWDEFAULT = 10;
    public const int SW_FORCEMINIMIZE = 11;

    /// <summary>
    /// 窗口放置信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;

        public static WINDOWPLACEMENT Default
        {
            get
            {
                var result = new WINDOWPLACEMENT();
                result.length = Marshal.SizeOf(result);
                return result;
            }
        }
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
    /// 设置分层窗口的属性（用于设置不透明度和透明色键）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="crKey">透明色键（RGB 值）</param>
    /// <param name="bAlpha">不透明度（0-255）</param>
    /// <param name="dwFlags">标志（LWA_COLORKEY 或 LWA_ALPHA）</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>
    /// SetLayeredWindowAttributes 的标志常量
    /// </summary>
    public const uint LWA_COLORKEY = 0x00000001;  // 使用透明色键
    public const uint LWA_ALPHA = 0x00000002;     // 使用 Alpha 通道（不透明度）
}

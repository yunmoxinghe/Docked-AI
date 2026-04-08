using System;
using System.Runtime.InteropServices;

namespace Docked_AI.功能.主窗口v2.服务层.Win32互操作;

/// <summary>
/// 焦点管理相关的 Win32 API 互操作
/// 提供窗口焦点获取、前台窗口设置、窗口闪烁等功能的 P/Invoke 声明
/// </summary>
public static class 焦点管理互操作
{
    /// <summary>
    /// 将窗口设置为前台窗口并尝试获得输入焦点
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 将窗口提升到 Z 轴顶部
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// 获取当前前台窗口
    /// </summary>
    /// <returns>前台窗口句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 获取窗口所属线程 ID 和进程 ID
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="lpdwProcessId">接收进程 ID 的变量</param>
    /// <returns>线程 ID</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// 闪烁窗口以吸引用户注意
    /// </summary>
    /// <param name="pwfi">闪烁信息结构</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    /// <summary>
    /// 激活窗口并显示
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <returns>成功返回 true，失败返回 false</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetActiveWindow(IntPtr hWnd);

    /// <summary>
    /// 获取活动窗口
    /// </summary>
    /// <returns>活动窗口句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    /// <summary>
    /// 设置焦点到指定窗口
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <returns>之前拥有焦点的窗口句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    /// <summary>
    /// 获取拥有焦点的窗口
    /// </summary>
    /// <returns>拥有焦点的窗口句柄</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetFocus();

    /// <summary>
    /// 附加线程输入（用于跨线程焦点管理，不推荐常规使用）
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    /// <summary>
    /// 允许设置前台窗口（用于解除前台锁定限制）
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    /// <summary>
    /// 锁定设置前台窗口（防止其他进程抢占前台）
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LockSetForegroundWindow(uint uLockCode);

    // ===== 常量定义 =====

    /// <summary>
    /// FlashWindowEx 标志
    /// </summary>
    public const uint FLASHW_STOP = 0;        // 停止闪烁
    public const uint FLASHW_CAPTION = 0x1;   // 闪烁标题栏
    public const uint FLASHW_TRAY = 0x2;      // 闪烁任务栏按钮
    public const uint FLASHW_ALL = 0x3;       // 闪烁标题栏和任务栏
    public const uint FLASHW_TIMER = 0x4;     // 持续闪烁直到窗口获得焦点
    public const uint FLASHW_TIMERNOFG = 0xC; // 持续闪烁直到用户点击

    /// <summary>
    /// LockSetForegroundWindow 常量
    /// </summary>
    public const uint LSFW_LOCK = 1;    // 锁定前台窗口
    public const uint LSFW_UNLOCK = 2;  // 解锁前台窗口

    /// <summary>
    /// AllowSetForegroundWindow 特殊值
    /// </summary>
    public const uint ASFW_ANY = unchecked((uint)-1);  // 允许任何进程设置前台窗口

    // ===== 结构定义 =====

    /// <summary>
    /// 闪烁窗口信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint cbSize;      // 结构大小
        public IntPtr hwnd;      // 窗口句柄
        public uint dwFlags;     // 闪烁标志
        public uint uCount;      // 闪烁次数（0 表示持续闪烁）
        public uint dwTimeout;   // 闪烁间隔（毫秒，0 表示使用默认值）

        public static FLASHWINFO Create(IntPtr hwnd, uint flags, uint count, uint timeout)
        {
            return new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = hwnd,
                dwFlags = flags,
                uCount = count,
                dwTimeout = timeout
            };
        }
    }
}

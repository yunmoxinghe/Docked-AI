using System;
using System.Runtime.InteropServices;

namespace DockedTools.Features.Tray
{
    /// <summary>
    /// 托盘唤起窗口时用到的 Win32 接口。
    /// 重点是"尽量把窗口从后台拉到用户面前，并真正拿到输入焦点"。
    /// </summary>
    internal static partial class TrayWin32Api
    {
        // 尝试把窗口切到前台。
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetForegroundWindow(IntPtr hWnd);

        // 读取当前正在最前面的那个窗口。
        [LibraryImport("user32.dll")]
        internal static partial IntPtr GetForegroundWindow();

        // 读取某个窗口所属线程。
        // 这里是为了知道"当前前台窗口"归哪个线程管。
        [LibraryImport("user32.dll")]
        internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // 临时把两个线程的输入队列接到一起。
        // 这样我们才更容易突破前台限制，把自己的窗口抢到前面。
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        // 把窗口提到顶层顺序。
        // 它更偏向 Z 轴顺序，不一定等于真正获得键盘焦点。
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool BringWindowToTop(IntPtr hWnd);

        // 改窗口显示状态。
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // 改窗口的位置/层级。
        // 这里主要拿来短暂置顶再取消置顶，帮助窗口"刷"到最前面。
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        // 获取当前代码正在运行的线程 ID。
        // 这样才能跟前台窗口线程做 AttachThreadInput。
        [LibraryImport("kernel32.dll")]
        internal static partial uint GetCurrentThreadId();

        // 显示窗口。
        internal const int SW_SHOW = 5;
        // 临时置顶。
        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        // 取消置顶。
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);
        // 不改大小。
        internal const uint SWP_NOSIZE = 0x0001;
        // 不改位置。
        internal const uint SWP_NOMOVE = 0x0002;
        // 顺手显示窗口。
        internal const uint SWP_SHOWWINDOW = 0x0040;
    }
}

using System;
using System.Runtime.InteropServices;
using Docked_AI.Features.MainWindow.Placement;

namespace Docked_AI.Features.MainWindow.Visibility
{
    /// <summary>
    /// 显示隐藏相关的 Win32 接口。
    /// 这里处理的是“窗口放到哪、显不显示、顶不顶层、接不接系统消息、占不占停靠栏”。
    /// </summary>
    internal static class VisibilityWin32Api
    {
        // 自定义窗口消息处理函数签名。
        // 人话就是“Windows 给你发消息时，你要按这个格式接电话”。
        internal delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // 把窗口提到最上层。
        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        // 取消最上层。
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

        // 改窗口的位置、大小、层级，或者顺便让它显示出来。
        // 这是控制窗口“摆在哪”的核心接口。
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        // 改窗口显示状态。
        // 比如隐藏、显示、还原，都是靠它。
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // 向系统注册一条“本应用专用消息”。
        // 这样 AppBar 之类的回调消息就不会跟别的消息号撞车。
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        // 改窗口某个长整型属性。
        // 这里专门拿来替换窗口过程，也就是接管窗口消息分发。
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 把消息继续交给旧的窗口过程处理。
        // 人话就是“这条消息我不自己吃掉，继续按系统原来的流程走”。
        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // 跟 Shell 交互 AppBar。
        // 也就是告诉系统“我这个窗口要像任务栏那样占据桌面边缘的一块区域”。
        [DllImport("shell32.dll")]
        internal static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        // 把当前窗口的消息处理函数替换成我们自己的。
        internal static IntPtr SetWindowProc(IntPtr hWnd, IntPtr newWindowProc)
        {
            return SetWindowLongPtr(hWnd, GWLP_WNDPROC, newWindowProc);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public PlacementWin32Api.RECT rc;
            public IntPtr lParam;
        }

        // 彻底隐藏窗口。
        internal const int SW_HIDE = 0;
        // 保持原大小不变。
        internal const uint SWP_NOSIZE = 0x0001;
        // 保持原位置不变。
        internal const uint SWP_NOMOVE = 0x0002;
        // 保持原 Z 轴层级不变。
        internal const uint SWP_NOZORDER = 0x0004;
        // 不抢焦点。
        internal const uint SWP_NOACTIVATE = 0x0010;
        // 执行 SetWindowPos 时顺手显示窗口。
        internal const uint SWP_SHOWWINDOW = 0x0040;
        // 不改拥有者窗口的层级。
        internal const uint SWP_NOOWNERZORDER = 0x0200;
        // 告诉系统“窗口边框规则变了，请重算非客户区”。
        internal const uint SWP_FRAMECHANGED = 0x0020;
        // 窗口过程指针所在的槽位。
        internal const int GWLP_WNDPROC = -4;
        // 注册一个新的 AppBar。
        internal const uint ABM_NEW = 0x00000000;
        // 移除 AppBar。
        internal const uint ABM_REMOVE = 0x00000001;
        // 先问系统“这个位置能不能占、该怎么修正”。
        internal const uint ABM_QUERYPOS = 0x00000002;
        // 正式把修正后的位置提交给系统。
        internal const uint ABM_SETPOS = 0x00000003;
        // 停靠在屏幕右边。
        internal const uint ABE_RIGHT = 2;
        // 系统在计算非客户区大小时发来的消息。
        internal const uint WM_NCCALCSIZE = 0x0083;
        // 系统准备绘制非客户区时发来的消息。
        internal const uint WM_NCPAINT = 0x0085;
        // 系统准备更新非客户区激活状态时发来的消息。
        internal const uint WM_NCACTIVATE = 0x0086;
    }
}

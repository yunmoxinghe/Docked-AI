using System;
using System.Runtime.InteropServices;
using Docked_AI.Features.MainWindow.Placement;

namespace Docked_AI.Features.MainWindow.Visibility
{
    internal static class VisibilityWin32Api
    {
        internal delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll")]
        internal static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

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

        internal const int SW_HIDE = 0;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint SWP_NOOWNERZORDER = 0x0200;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const int GWLP_WNDPROC = -4;
        internal const uint ABM_NEW = 0x00000000;
        internal const uint ABM_REMOVE = 0x00000001;
        internal const uint ABM_QUERYPOS = 0x00000002;
        internal const uint ABM_SETPOS = 0x00000003;
        internal const uint ABE_RIGHT = 2;
        internal const uint WM_NCCALCSIZE = 0x0083;
        internal const uint WM_NCPAINT = 0x0085;
        internal const uint WM_NCACTIVATE = 0x0086;
    }
}

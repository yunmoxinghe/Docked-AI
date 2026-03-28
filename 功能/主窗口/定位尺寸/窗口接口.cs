using System;
using System.Runtime.InteropServices;

namespace Docked_AI.Features.MainWindow.Placement
{
    internal static class Win32WindowApi
    {
        internal delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern bool SystemParametersInfo(int uAction, int uParam, ref RECT lpvParam, int fuWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("shell32.dll")]
        internal static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out uint pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NCCALCSIZE_PARAMS
        {
            public RECT rgrc0;
            public RECT rgrc1;
            public RECT rgrc2;
            public IntPtr lppos;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        internal const int SW_HIDE = 0;
        internal const int SW_SHOWNOACTIVATE = 4;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint SWP_NOOWNERZORDER = 0x0200;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;
        internal const int SPI_GETWORKAREA = 0x0030;
        internal const int GWL_STYLE = -16;
        internal const int GWL_EXSTYLE = -20;
        internal const int GWLP_WNDPROC = -4;
        internal const int WS_OVERLAPPED = 0x00000000;
        internal const int WS_POPUP = unchecked((int)0x80000000);
        internal const int WS_VISIBLE = 0x10000000;
        internal const int WS_CAPTION = 0x00C00000;
        internal const int WS_THICKFRAME = 0x00040000;
        internal const int WS_BORDER = 0x00800000;
        internal const int WS_DLGFRAME = 0x00400000;
        internal const int WS_SYSMENU = 0x00080000;
        internal const int WS_MINIMIZEBOX = 0x00020000;
        internal const int WS_MAXIMIZEBOX = 0x00010000;
        internal const int WS_OVERLAPPEDWINDOW =
            WS_OVERLAPPED |
            WS_CAPTION |
            WS_SYSMENU |
            WS_THICKFRAME |
            WS_MINIMIZEBOX |
            WS_MAXIMIZEBOX;
        internal const int WS_EX_DLGMODALFRAME = 0x00000001;
        internal const int WS_EX_WINDOWEDGE = 0x00000100;
        internal const int WS_EX_CLIENTEDGE = 0x00000200;
        internal const int WS_EX_STATICEDGE = 0x00020000;
        internal const uint ABM_NEW = 0x00000000;
        internal const uint ABM_REMOVE = 0x00000001;
        internal const uint ABM_QUERYPOS = 0x00000002;
        internal const uint ABM_SETPOS = 0x00000003;
        internal const uint ABE_RIGHT = 2;
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        internal const int DWMWA_BORDER_COLOR = 34;
        internal const int DWMWA_CAPTION_COLOR = 35;
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        internal const int DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37;
        internal const int DWMWCP_DEFAULT = 0;
        internal const int DWMWCP_DONOTROUND = 1;
        internal const int DWMWA_COLOR_DEFAULT = -1;
        internal const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        internal const uint WM_NCCALCSIZE = 0x0083;
        internal const uint WM_NCPAINT = 0x0085;
        internal const uint WM_NCACTIVATE = 0x0086;
    }
}

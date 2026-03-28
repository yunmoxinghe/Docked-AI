using System;
using System.Runtime.InteropServices;

namespace Docked_AI.Features.MainWindow.Placement
{
    internal static class Win32WindowApi
    {
        internal static readonly IntPtr HWND_TOPMOST = new(-1);
        internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

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

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
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
        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;
        internal const int SPI_GETWORKAREA = 0x0030;
        internal const uint ABM_NEW = 0x00000000;
        internal const uint ABM_REMOVE = 0x00000001;
        internal const uint ABM_QUERYPOS = 0x00000002;
        internal const uint ABM_SETPOS = 0x00000003;
        internal const uint ABE_RIGHT = 2;
    }
}

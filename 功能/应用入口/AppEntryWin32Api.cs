using System;
using System.Runtime.InteropServices;

namespace Docked_AI.Features.AppEntry
{
    internal static class AppEntryWin32Api
    {
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        internal const int SW_SHOWNORMAL = 1;
        internal const int SW_HIDE = 0;
    }
}

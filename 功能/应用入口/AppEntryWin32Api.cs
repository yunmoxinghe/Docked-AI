using System;
using System.Runtime.InteropServices;

namespace Docked_AI.Features.AppEntry
{
    /// <summary>
    /// 应用入口阶段用到的最小 Win32 接口集。
    /// 这里主要做的是“发现已有实例后，把它叫出来并切到前台”。
    /// </summary>
    internal static class AppEntryWin32Api
    {
        // 改窗口显示状态。
        // 这里主要用来把已存在实例的窗口恢复出来，或者把保活窗口藏起来。
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // 尝试把某个窗口切到前台。
        // 人话就是“让用户眼前看到它，并把输入焦点给它”。
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        // 普通显示/还原窗口。
        internal const int SW_SHOWNORMAL = 1;
        // 隐藏窗口。
        internal const int SW_HIDE = 0;
    }
}

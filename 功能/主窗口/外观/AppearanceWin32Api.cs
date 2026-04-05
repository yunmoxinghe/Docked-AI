using System;
using System.Runtime.InteropServices;
using Docked_AI.Features.MainWindow.Placement;

namespace Docked_AI.Features.MainWindow.Appearance
{
    /// <summary>
    /// 外观相关的 Win32 接口。
    /// 这里处理的是边框、标题栏、圆角、DWM 外观属性这些“看起来像什么”的问题。
    /// </summary>
    internal static class AppearanceWin32Api
    {
        // 读取窗口当前样式。
        // 比如这个窗口现在有没有标题栏、边框、可否拉伸，都是从这里拿。
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        // 覆盖窗口样式。
        // 比如把普通窗口改成无边框窗口，就是在这里动手。
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 设置 DWM 的窗口外观属性。
        // 这里主要拿来改圆角、边框颜色、标题栏颜色。
        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        // 让 DWM 的窗口框架往客户区延伸。
        // 人话就是“把系统边框那层视觉效果推进到内容区里”，方便做无边框效果。
        [DllImport("dwmapi.dll")]
        internal static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        // 读取 DWM 计算后的窗口外框。
        // 这里用它拿真实可见边界，避免看起来对齐了，实际上还差几个像素。
        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out PlacementWin32Api.RECT pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        // 读取普通窗口样式。
        internal const int GWL_STYLE = -16;
        // 读取扩展窗口样式。
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_OVERLAPPED = 0x00000000;
        // 弹出式窗口样式，常用于无标题栏、无边框窗口。
        internal const int WS_POPUP = unchecked((int)0x80000000);
        // 窗口可见。
        internal const int WS_VISIBLE = 0x10000000;
        // 标题栏。
        internal const int WS_CAPTION = 0x00C00000;
        // 可拉伸边框。
        internal const int WS_THICKFRAME = 0x00040000;
        // 普通边框。
        internal const int WS_BORDER = 0x00800000;
        // 对话框边框。
        internal const int WS_DLGFRAME = 0x00400000;
        // 系统菜单。
        internal const int WS_SYSMENU = 0x00080000;
        // 最小化按钮。
        internal const int WS_MINIMIZEBOX = 0x00020000;
        // 最大化按钮。
        internal const int WS_MAXIMIZEBOX = 0x00010000;
        // 一整个“标准桌面窗口”的常见样式组合。
        internal const int WS_OVERLAPPEDWINDOW =
            WS_OVERLAPPED |
            WS_CAPTION |
            WS_SYSMENU |
            WS_THICKFRAME |
            WS_MINIMIZEBOX |
            WS_MAXIMIZEBOX;
        // 扩展样式：对话框边框感。
        internal const int WS_EX_DLGMODALFRAME = 0x00000001;
        // 扩展样式：窗口外沿阴影/边线。
        internal const int WS_EX_WINDOWEDGE = 0x00000100;
        // 扩展样式：客户区凹边。
        internal const int WS_EX_CLIENTEDGE = 0x00000200;
        // 扩展样式：静态边框。
        internal const int WS_EX_STATICEDGE = 0x00020000;
        // DWM 属性：窗口圆角偏好。
        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        // DWM 属性：边框颜色。
        internal const int DWMWA_BORDER_COLOR = 34;
        // DWM 属性：标题栏颜色。
        internal const int DWMWA_CAPTION_COLOR = 35;
        // DWM 属性：真实扩展边框范围。
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        // 正常使用系统默认圆角策略。
        internal const int DWMWCP_DEFAULT = 0;
        // 不要圆角。
        internal const int DWMWCP_DONOTROUND = 1;
        // 边框颜色使用系统默认值。
        internal const int DWMWA_COLOR_DEFAULT = -1;
        // 边框颜色设为“无”。
        internal const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
    }
}

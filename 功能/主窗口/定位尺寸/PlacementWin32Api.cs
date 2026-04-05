using System.Runtime.InteropServices;

namespace Docked_AI.Features.MainWindow.Placement
{
    /// <summary>
    /// 定位尺寸相关的 Win32 接口。
    /// 这里关注的是“屏幕有多大、工作区多大”，不处理窗口外观和显示状态。
    /// </summary>
    internal static class PlacementWin32Api
    {
        // 读取系统尺寸信息。
        // 这里主要用它拿整块屏幕的宽高。
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        // 读取系统参数。
        // 这里传 SPI_GETWORKAREA 时，意思是“告诉我桌面里真正可用的工作区”，
        // 也就是扣掉任务栏后的那块区域。
        [DllImport("user32.dll")]
        internal static extern bool SystemParametersInfo(int uAction, int uParam, ref RECT lpvParam, int fuWinIni);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // 整个屏幕的宽度。
        internal const int SM_CXSCREEN = 0;
        // 整个屏幕的高度。
        internal const int SM_CYSCREEN = 1;
        // 获取工作区，也就是“任务栏之外真正能放窗口的区域”。
        internal const int SPI_GETWORKAREA = 0x0030;
    }
}

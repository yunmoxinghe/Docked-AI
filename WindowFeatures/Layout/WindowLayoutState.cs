using Docked_AI.WindowFeatures.Native;

namespace Docked_AI.WindowFeatures.Layout
{
    internal sealed class WindowLayoutState
    {
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public Win32WindowApi.RECT WorkArea;
        public int WindowWidth { get; set; } = 500;
        public int WindowHeight { get; set; }
        public int Margin { get; set; } = 10;
        public int TargetX { get; set; }
        public double TargetY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
    }
}

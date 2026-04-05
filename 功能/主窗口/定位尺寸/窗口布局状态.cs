using Docked_AI.Features.MainWindow.Placement;

namespace Docked_AI.Features.MainWindow.Placement
{
    internal sealed class WindowLayoutState
    {
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public PlacementWin32Api.RECT WorkArea;
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int MinWindowWidth { get; set; } = 380;
        public int Margin { get; set; } = 10;
        public int TargetX { get; set; }
        public double TargetY { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
    }
}

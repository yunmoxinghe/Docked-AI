using Docked_AI.Features.MainWindow.Native;

namespace Docked_AI.Features.MainWindow.Layout
{
    internal sealed class WindowLayoutService
    {
        public WindowLayoutState CreateInitialState()
        {
            var state = new WindowLayoutState();
            Refresh(state);
            return state;
        }

        public void Refresh(WindowLayoutState state)
        {
            state.ScreenHeight = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CYSCREEN);
            state.ScreenWidth = Win32WindowApi.GetSystemMetrics(Win32WindowApi.SM_CXSCREEN);
            Win32WindowApi.SystemParametersInfo(Win32WindowApi.SPI_GETWORKAREA, 0, ref state.WorkArea, 0);

            state.WindowHeight = state.WorkArea.Bottom - state.WorkArea.Top - (state.Margin * 2);
            state.TargetX = state.WorkArea.Right - state.WindowWidth - state.Margin;
            state.TargetY = state.WorkArea.Top + state.Margin;
            state.CurrentY = state.TargetY;
        }

        public void PrepareForShow(WindowLayoutState state)
        {
            Refresh(state);
            state.CurrentX = state.ScreenWidth;
        }

        public void PrepareForHide(WindowLayoutState state)
        {
            Refresh(state);

            if (state.CurrentX == 0)
            {
                state.CurrentX = state.TargetX;
            }
        }
    }
}

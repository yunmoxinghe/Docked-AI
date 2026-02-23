using System;

namespace Docked_AI.Features.MainWindow.Placement
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

            int availableWidth = state.WorkArea.Right - state.WorkArea.Left - (state.Margin * 2);
            if (state.WindowWidth <= 0)
            {
                state.WindowWidth = availableWidth / 3;
            }

            state.WindowWidth = Math.Max(state.MinWindowWidth, state.WindowWidth);
            state.WindowWidth = Math.Min(availableWidth, state.WindowWidth);
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

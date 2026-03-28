using Docked_AI.Features.MainWindow.State;

namespace Docked_AI.Features.MainWindow.State
{
    public sealed class MainWindowViewModel : ObservableObject
    {
        private bool _isWindowVisible = true;
        private bool _isDockPinned;

        public bool IsWindowVisible
        {
            get => _isWindowVisible;
            private set => SetProperty(ref _isWindowVisible, value);
        }

        public bool IsDockPinned
        {
            get => _isDockPinned;
            private set => SetProperty(ref _isDockPinned, value);
        }

        public void MarkVisible()
        {
            IsWindowVisible = true;
        }

        public void MarkHidden()
        {
            IsWindowVisible = false;
        }

        public void SetDockPinned(bool isDockPinned)
        {
            IsDockPinned = isDockPinned;
        }
    }
}

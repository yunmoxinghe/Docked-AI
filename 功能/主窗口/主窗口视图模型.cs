using Docked_AI.Features.MainWindow;

namespace Docked_AI.Features.MainWindow
{
    public sealed class MainWindowViewModel : ObservableObject
    {
        private bool _isWindowVisible = true;

        public bool IsWindowVisible
        {
            get => _isWindowVisible;
            private set => SetProperty(ref _isWindowVisible, value);
        }

        public void MarkVisible()
        {
            IsWindowVisible = true;
        }

        public void MarkHidden()
        {
            IsWindowVisible = false;
        }
    }
}

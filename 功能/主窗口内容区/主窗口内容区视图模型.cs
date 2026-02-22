using Docked_AI.Features.MainWindow.State;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed class MainWindowContentViewModel : ObservableObject
    {
        private string _title = "Main Content Area";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }
}

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

        public void SelectSection(int sectionIndex)
        {
            Title = sectionIndex switch
            {
                0 => "Main Content Area",
                1 => "Workspace",
                2 => "Settings",
                _ => "Main Content Area"
            };
        }
    }
}

using Docked_AI.Features.MainWindow.State;
using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.Settings;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed class MainWindowContentViewModel : ObservableObject
    {
        private HomePage? _homePage;
        private SettingsPage? _settingsPage;
        private object? _currentPage;

        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public MainWindowContentViewModel()
        {
            SelectSection(0);
        }

        public void SelectSection(int sectionIndex)
        {
            if (sectionIndex < 0)
            {
                sectionIndex = 0;
            }

            CurrentPage = sectionIndex switch
            {
                1 => _settingsPage ??= new SettingsPage(),
                _ => _homePage ??= new HomePage()
            };
        }
    }
}

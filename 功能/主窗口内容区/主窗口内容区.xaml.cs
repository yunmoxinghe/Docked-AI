using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed partial class MainWindowContent : UserControl
    {
        public MainWindowContentViewModel ViewModel { get; }

        public MainWindowContent()
        {
            InitializeComponent();
            ViewModel = new MainWindowContentViewModel();
            DataContext = ViewModel;
        }

        private void RightNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ViewModel.SelectSection(1);
                return;
            }

            if (args.SelectedItemContainer?.Tag is string tagText && int.TryParse(tagText, out int sectionIndex))
            {
                ViewModel.SelectSection(sectionIndex);
            }
        }
    }
}

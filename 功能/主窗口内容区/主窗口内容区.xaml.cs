using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.Settings;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed partial class MainWindowContent : UserControl
    {
        public MainWindowContent()
        {
            InitializeComponent();
            // Navigate to home page on initialization
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void RightNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer?.Tag is string tagText && int.TryParse(tagText, out int sectionIndex))
            {
                switch (sectionIndex)
                {
                    case 0:
                        ContentFrame.Navigate(typeof(HomePage));
                        break;
                    default:
                        ContentFrame.Navigate(typeof(HomePage));
                        break;
                }
            }
        }
    }
}


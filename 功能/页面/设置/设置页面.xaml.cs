using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.Pages.Settings
{
    public sealed partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += OnLayoutChanged;
            SizeChanged += OnLayoutChanged;
        }

        private void OnLayoutChanged(object sender, RoutedEventArgs e)
        {
            ApplyResponsivePadding();
        }

        private void OnLayoutChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsivePadding();
        }

        private void ApplyResponsivePadding()
        {
            double horizontal = ActualWidth >= 500 ? 36 : 16;
            RootGrid.Padding = new Thickness(horizontal, 0, horizontal, 0);
        }
    }
}

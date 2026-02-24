using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Docked_AI.Features.Pages.Settings
{
    public sealed partial class SettingsPage : Page
    {
        private const double WideThreshold = 500;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisualStateAndDiagnostic();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualStateAndDiagnostic();
        }

        private void UpdateVisualStateAndDiagnostic()
        {
            double width = ActualWidth;
            if (width <= 0 && RootGrid != null)
            {
                width = RootGrid.ActualWidth;
            }
            bool isWide = width >= WideThreshold;
            string stateName = isWide ? "WideState" : "NarrowState";

            // Drive visual state from UserControl width (not Window width).
            _ = VisualStateManager.GoToState(this, stateName, false);

            WidthValueText.Text = $"{width:F0}px | threshold {WideThreshold:F0}px | {stateName} | margin {CardsPanel.Margin.Left:F0}";
        }
    }
}

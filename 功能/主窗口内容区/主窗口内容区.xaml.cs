using Docked_AI.Features.Pages.Home;
using Docked_AI.Features.Pages.Settings;
using Docked_AI.Features.Pages.Test;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

namespace Docked_AI.Features.MainWindowContent
{
    public sealed partial class MainWindowContent : UserControl
    {
        private const float ContentCornerRadius = 4f;

        private CompositionRoundedRectangleGeometry? _clipGeometry;

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
                    case 1:
                        ContentFrame.Navigate(typeof(ClipTestPage));
                        break;
                    default:
                        ContentFrame.Navigate(typeof(HomePage));
                        break;
                }
            }
        }

        private void ContentFrame_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(ContentFrame);
            if (_clipGeometry == null)
            {
                var compositor = visual.Compositor;
                _clipGeometry = compositor.CreateRoundedRectangleGeometry();
                _clipGeometry.CornerRadius = new Vector2(ContentCornerRadius, ContentCornerRadius);
                _clipGeometry.Offset = Vector2.Zero;
                visual.Clip = compositor.CreateGeometricClip(_clipGeometry);
            }

            _clipGeometry.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }
}

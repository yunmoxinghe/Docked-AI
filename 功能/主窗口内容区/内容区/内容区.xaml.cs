using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Numerics;

namespace Docked_AI.Features.MainWindowContent.ContentArea
{
    public sealed partial class ContentArea : UserControl
    {
        private const float ContentCornerRadius = 4f;
        private CompositionRoundedRectangleGeometry? _clipGeometry;

        public event EventHandler<NavigationEventArgs>? Navigated;

        public ContentArea()
        {
            InitializeComponent();
            ContentFrame.Navigated += ContentFrame_Navigated;
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            Navigated?.Invoke(this, e);
        }

        public void Navigate(Type pageType, object? parameter = null)
        {
            if (parameter != null)
            {
                ContentFrame.Navigate(pageType, parameter);
            }
            else
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void ContentFrame_SizeChanged(object sender, SizeChangedEventArgs e)
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

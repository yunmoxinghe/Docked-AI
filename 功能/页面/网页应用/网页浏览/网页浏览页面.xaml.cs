using Docked_AI.Features.Pages.WebApp.Shared;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    public sealed partial class WebBrowserPage : Page
    {
        private const string PreferredWebViewLanguage = "zh-CN";
        private const double WebViewCornerRadius = 8;

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private Uri? _currentUri;
        private CompositionGeometricClip? _webViewClip;
        private CompositionRoundedRectangleGeometry? _webViewClipGeometry;

        public WebBrowserPage()
        {
            InitializeComponent();
            WebView.SizeChanged += WebView_SizeChanged;
            Loaded += WebBrowserPage_Loaded;
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is not WebAppShortcut shortcut)
            {
                return;
            }

            if (!Uri.TryCreate(shortcut.Url, UriKind.Absolute, out Uri? uri))
            {
                return;
            }

            if (_isWebViewReady && _currentUri is not null && UriEquals(_currentUri, uri))
            {
                return;
            }

            _currentUri = uri;
            _pendingNavigationUri = uri;
            TryNavigatePendingUri();
        }

        private async void WebBrowserPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WebBrowserPage_Loaded;
            ApplyWebViewCornerClip();
            await EnsureWebViewInitializedAsync();
            TryNavigatePendingUri();
        }

        private async Task EnsureWebViewInitializedAsync()
        {
            if (_isWebViewReady)
            {
                return;
            }

            try
            {
                CoreWebView2EnvironmentOptions options = new()
                {
                    Language = GetWebViewLanguage()
                };
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: null,
                    options: options);
                await WebView.EnsureCoreWebView2Async(environment);
            }
            catch
            {
                // Fall back to default initialization behavior.
            }
            finally
            {
                _isWebViewReady = true;
            }
        }

        private void TryNavigatePendingUri()
        {
            if (!_isWebViewReady || _pendingNavigationUri is null)
            {
                return;
            }

            if (WebView.Source is not null && UriEquals(WebView.Source, _pendingNavigationUri))
            {
                _pendingNavigationUri = null;
                return;
            }

            WebView.Source = _pendingNavigationUri;
            _pendingNavigationUri = null;
        }

        private void ApplyWebViewCornerClip()
        {
            var visual = ElementCompositionPreview.GetElementVisual(WebView);
            Compositor compositor = visual.Compositor;

            _webViewClipGeometry ??= compositor.CreateRoundedRectangleGeometry();
            _webViewClipGeometry.CornerRadius = new Vector2((float)WebViewCornerRadius);
            _webViewClipGeometry.Offset = Vector2.Zero;

            _webViewClip ??= compositor.CreateGeometricClip(_webViewClipGeometry);
            visual.Clip = _webViewClip;

            UpdateWebViewCornerClipSize();
        }

        private void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWebViewCornerClipSize();
        }

        private void UpdateWebViewCornerClipSize()
        {
            if (_webViewClipGeometry is null)
            {
                return;
            }

            float width = (float)Math.Max(0, WebView.ActualWidth);
            float height = (float)Math.Max(0, WebView.ActualHeight);
            _webViewClipGeometry.Size = new Vector2(width, height);
        }

        private static string GetWebViewLanguage()
        {
            string uiLanguage = CultureInfo.CurrentUICulture.Name;
            if (uiLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return uiLanguage;
            }

            return PreferredWebViewLanguage;
        }

        private static bool UriEquals(Uri left, Uri right)
        {
            return Uri.Compare(
                       left,
                       right,
                       UriComponents.AbsoluteUri,
                       UriFormat.SafeUnescaped,
                       StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}

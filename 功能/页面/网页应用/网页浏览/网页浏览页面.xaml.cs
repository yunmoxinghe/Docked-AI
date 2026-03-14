using Docked_AI.Features.Pages.WebApp.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    public sealed partial class WebBrowserPage : Page
    {
        private const string PreferredWebViewLanguage = "zh-CN";
        private const string CornerClipScript = @"
(() => {
  const id = '__docked_ai_corner_style__';
  if (document.getElementById(id)) return;
  const style = document.createElement('style');
  style.id = id;
  style.textContent = `
    html, body {
      border-radius: 8px !important;
      overflow: hidden !important;
      background-clip: padding-box !important;
    }
  `;
  document.head.appendChild(style);
})();";

        private Uri? _pendingNavigationUri;
        private bool _isWebViewReady;
        private Uri? _currentUri;

        public WebBrowserPage()
        {
            InitializeComponent();
            WebView.CoreWebView2Initialized += WebView_CoreWebView2Initialized;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
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

        private void WebView_CoreWebView2Initialized(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (args.Exception is not null || sender.CoreWebView2 is null)
            {
                return;
            }

            _ = sender.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(CornerClipScript);
        }

        private void WebView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess || sender.CoreWebView2 is null)
            {
                return;
            }

            _ = sender.CoreWebView2.ExecuteScriptAsync(CornerClipScript);
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

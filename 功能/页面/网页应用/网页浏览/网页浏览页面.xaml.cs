using Docked_AI.Features.Pages.WebApp.Shared;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;

namespace Docked_AI.Features.Pages.WebApp.Browser
{
    public sealed partial class WebBrowserPage : Page
    {
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

        public WebBrowserPage()
        {
            InitializeComponent();
            WebView.CoreWebView2Initialized += WebView_CoreWebView2Initialized;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
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

            WebView.Source = uri;
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
    }
}

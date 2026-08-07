using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 上下文菜单部分
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void OnWinUIContextMenuSettingsChanged(object? sender, EventArgs e)
        {
            bool useWinUIContextMenu = ExperimentalSettings.EnableWinUIContextMenu;
            if (WebView?.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = !useWinUIContextMenu;
                UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
            }
        }

        private void UpdateContextMenuConfiguration(bool useWinUIContextMenu)
        {
            UpdateContextMenuForWebView(WebView, useWinUIContextMenu);
        }

        private void UpdateContextMenuForWebView(Microsoft.UI.Xaml.Controls.WebView2 webView, bool useWinUIContextMenu)
        {
            if (webView == null) return;

            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                if (useWinUIContextMenu)
                {
                    webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                }
            }

            if (useWinUIContextMenu)
            {
                if (webView.ContextFlyout == null && webView == WebView)
                {
                    webView.ContextFlyout = WebViewContextMenu;
                }
            }
            else
            {
                webView.ContextFlyout = null;
            }
        }

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            e.MenuItems.Clear();
            _contextMenuLinkUrl = e.ContextMenuTarget.LinkUri;
            _contextMenuSelectedText = e.ContextMenuTarget.SelectionText;

            CopyMenuItem.IsEnabled = true;
            CopyLinkMenuItem.IsEnabled = !string.IsNullOrEmpty(_contextMenuLinkUrl);

            var flyout = WebView?.ContextFlyout as MenuFlyout;
            if (flyout != null)
            {
                flyout.ShowAt(WebView, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
                {
                    Position = new Windows.Foundation.Point(e.Location.X, e.Location.Y)
                });
            }
        }

        private void BackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoBack) WebView.GoBack();
        }

        private void ForwardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoForward) WebView.GoForward();
        }

        private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TryReloadWebView();
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_contextMenuSelectedText))
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(_contextMenuSelectedText);
                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();
                    return;
                }

                if (WebView?.CoreWebView2 != null)
                {
                    string script = "window.getSelection().toString()";
                    string result = await WebView.CoreWebView2.ExecuteScriptAsync(script);

                    string? selectedText = null;
                    if (result.Length >= 2 && result.StartsWith("\"") && result.EndsWith("\""))
                    {
                        using var document = JsonDocument.Parse(result);
                        selectedText = document.RootElement.GetString();
                    }
                    else if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        selectedText = result;
                    }

                    if (!string.IsNullOrEmpty(selectedText))
                    {
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(selectedText);
                        Clipboard.SetContent(dataPackage);
                        Clipboard.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy failed: {ex.Message}");
            }
        }

        private void CopyLinkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_contextMenuLinkUrl)) return;

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(_contextMenuLinkUrl);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy link failed: {ex.Message}");
            }
        }

        private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null) return;

            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(uri.AbsoluteUri);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Copy URL failed: {ex.Message}");
            }
        }

        private async void OpenExternalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null) return;

            var dialog = CreateExternalOpenDialog(uri);
            var result = await Features.UnifiedCalls.InAppDialog.InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(uri.AbsoluteUri);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }

        private async void OpenExternalButton_Click(object sender, RoutedEventArgs e)
        {
            Uri? uri = WebView?.Source;
            if (uri is null) return;

            var dialog = CreateExternalOpenDialog(uri);
            var result = await Features.UnifiedCalls.InAppDialog.InAppDialogService.ShowAsync(dialog, this);
            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private static Features.UnifiedCalls.InAppDialog.UnifiedInAppDialog CreateExternalOpenDialog(Uri uri)
        {
            var dialog = new Features.UnifiedCalls.InAppDialog.UnifiedInAppDialog();
            dialog.Configure(
                Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_Title"),
                new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_Content"),
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14
                        },
                        new TextBlock
                        {
                            Text = uri.AbsoluteUri,
                            TextWrapping = TextWrapping.WrapWholeWords,
                            IsTextSelectionEnabled = true,
                            Opacity = 0.72,
                            FontSize = 12
                        }
                    }
                },
                Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_OpenButton"),
                Features.Localization.LocalizationHelper.GetString("InAppDialog_OpenExternal_CancelButton"),
                defaultButton: ContentDialogButton.Primary);
            return dialog;
        }
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - UI 和导航按钮部分
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        // ⚠️ InitializeTopBar、SetupTopBar、UpdateTopBarContent已移至 网页浏览页面.TopBar.cs

        // ⚠️ InitializeBottomBarReactor、UpdateBottomBarLayout、UpdateNavigationButtonStates已移至 网页浏览页面.BottomBar.cs

        // ⚠️ ShowShortcutIconAsync、ShowFallbackIcon已移至 网页浏览页面.Helpers.cs

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoBack) WebView.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView != null && WebView.CanGoForward) WebView.GoForward();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            TryReloadWebView();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut != null)
            {
                System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 关闭: {_currentShortcut.Id}");
                PageCloseRequested?.Invoke(this, _currentShortcut.Id);
            }
        }

        // ⚠️ PageCloseRequested事件已移至 网页浏览页面.Events.cs

        private void SetupRightContent()
        {
            var container = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

            if (_currentShortcut?.RightButton.IsEnabled == true)
            {
                SetupRightMappingButton();
                if (_rightMappingButton != null) container.Children.Add(_rightMappingButton);
            }

            if (!ExperimentalSettings.HideWebViewCloseButton)
            {
                _unpinButton = new Button
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(4),
                    Content = new FontIcon { Glyph = "\uE733", FontSize = 16, Foreground = _topBarForegroundBrush }
                };

                var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
                _unpinButton.Background = new SolidColorBrush(transparentColor);
                _unpinButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

                var resources = new ResourceDictionary();
                var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
                var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
                resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
                resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
                _unpinButton.Resources = resources;

                ToolTipService.SetToolTip(_unpinButton, "关闭");
                _unpinButton.Click += CloseButton_Click;
                container.Children.Add(_unpinButton);
            }

            Features.UnifiedCalls.TopAppBar.TopAppBarService.SetRightContent(container.Children.Count > 0 ? container : null);
        }
    }
}

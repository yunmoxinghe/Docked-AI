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
        private void InitializeTopBar()
        {
            _topBarContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8
            };

            var iconViewbox = new Viewbox { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            var iconGrid = new Grid { Width = 16, Height = 16 };

            _topBarIcon = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
            _topBarIconFallback = new FontIcon
            {
                Glyph = "\uE774",
                Width = 16,
                Height = 16,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            iconGrid.Children.Add(_topBarIcon);
            iconGrid.Children.Add(_topBarIconFallback);
            iconViewbox.Child = iconGrid;

            _topBarTitle = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300
            };

            _topBarContent.Children.Add(iconViewbox);
            _topBarContent.Children.Add(_topBarTitle);
        }

        private void SetupTopBar()
        {
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] SetupTopBar");
            Features.UnifiedCalls.TopAppBar.TopAppBarService.SetCenterContent(_topBarContent);
            SetupLeftMappingButton();
            SetupRightContent();
            Features.UnifiedCalls.TopAppBar.TopAppBarService.SetForeground(_topBarForegroundBrush);
            Features.UnifiedCalls.TopAppBar.TopAppBarService.IsVisible = true;
            Features.UnifiedCalls.TopAppBar.TopAppBarService.SetChromeVisible(false);
            UpdateTopBarContent();
        }

        private void UpdateTopBarContent()
        {
            if (_topBarTitle != null && _currentShortcut != null)
            {
                if (WebView?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(WebView.CoreWebView2.DocumentTitle))
                {
                    _topBarTitle.Text = WebView.CoreWebView2.DocumentTitle;
                }
                else
                {
                    _topBarTitle.Text = string.IsNullOrWhiteSpace(_currentShortcut.Name)
                        ? (_pendingNavigationUri?.Host ?? _currentShortcut.Url)
                        : _currentShortcut.Name;
                }
            }

            if (_currentShortcut != null && _currentShortcut.IconBytes != null && _currentShortcut.IconBytes.Length > 0)
            {
                _ = ShowShortcutIconAsync(_currentShortcut.IconBytes);
            }
        }

        private void InitializeBottomBarReactor()
        {
            _reactorHostControl = new Microsoft.UI.Reactor.Hosting.ReactorHostControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _bottomButtonBarComponent = new Components.BottomButtonBar
            {
                ButtonWidth = 48.0,
                CanGoBack = false,
                CanGoForward = false,
                OnBackClick = () => BackButton_Click(null!, null!),
                OnForwardClick = () => ForwardButton_Click(null!, null!),
                OnRefreshClick = () => RefreshButton_Click(null!, null!),
                OnCopyUrlClick = () => CopyUrlButton_Click(null!, null!),
                OnOpenExternalClick = () => OpenExternalButton_Click(null!, null!)
            };

            _reactorHostControl.Mount(_bottomButtonBarComponent);
            BottomButtonsContainer.Children.Add(_reactorHostControl);
        }

        private void UpdateBottomBarLayout()
        {
            if (BottomBarHost.ActualWidth <= 0 || _bottomButtonBarComponent == null) return;

            const int buttonCount = 5;
            const double minButtonWidth = 40.0;
            const double maxButtonWidth = 68.0;
            const double fixedHorizontalSpacing = 4.0;

            double availableWidth = BottomBarHost.ActualWidth;
            double totalSpacing = fixedHorizontalSpacing * (buttonCount + 1);
            double widthForButtons = availableWidth - totalSpacing;
            double buttonWidth = widthForButtons / buttonCount;
            buttonWidth = Math.Max(minButtonWidth, Math.Min(maxButtonWidth, buttonWidth));

            _bottomButtonBarComponent.ButtonWidth = buttonWidth;
            _reactorHostControl?.Mount(_bottomButtonBarComponent);
        }

        private void UpdateNavigationButtonStates()
        {
            if (_bottomButtonBarComponent == null || _reactorHostControl == null) return;

            bool canGoBack = WebView?.CanGoBack ?? false;
            bool canGoForward = WebView?.CanGoForward ?? false;

            _bottomButtonBarComponent.CanGoBack = canGoBack;
            _bottomButtonBarComponent.CanGoForward = canGoForward;
            _reactorHostControl.Mount(_bottomButtonBarComponent);
        }

        private async Task ShowShortcutIconAsync(byte[]? iconBytes)
        {
            if (iconBytes is not { Length: > 0 })
            {
                ShowFallbackIcon();
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(iconBytes.AsBuffer());
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream);

                if (_topBarIcon != null)
                {
                    _topBarIcon.Source = bitmap;
                    _topBarIcon.Visibility = Visibility.Visible;
                }
                if (_topBarIconFallback != null)
                {
                    _topBarIconFallback.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                ShowFallbackIcon();
            }
        }

        private void ShowFallbackIcon()
        {
            if (_topBarIcon != null)
            {
                _topBarIcon.Source = null;
                _topBarIcon.Visibility = Visibility.Collapsed;
            }
            if (_topBarIconFallback != null)
            {
                _topBarIconFallback.Visibility = Visibility.Visible;
            }
        }

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

        public event EventHandler<string>? PageCloseRequested;

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

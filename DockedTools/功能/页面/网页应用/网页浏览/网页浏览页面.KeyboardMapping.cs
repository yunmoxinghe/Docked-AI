using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 键盘映射按钮部分
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void SetupLeftMappingButton()
        {
            if (_currentShortcut == null)
            {
                Features.UnifiedCalls.TopAppBar.TopAppBarService.SetLeftContent(null);
                _leftMappingButton = null;
                return;
            }

            var config = _currentShortcut.LeftButton;
            if (!config.IsEnabled)
            {
                Features.UnifiedCalls.TopAppBar.TopAppBarService.SetLeftContent(null);
                _leftMappingButton = null;
                return;
            }

            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                icon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
            }

            _leftMappingButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Content = icon
            };

            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _leftMappingButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(transparentColor);
            _leftMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(tertiaryColor);
            _leftMappingButton.Resources = resources;

            ToolTipService.SetToolTip(_leftMappingButton, config.Tooltip);
            _leftMappingButton.Click += OnLeftMappingButtonClick;

            var container = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            container.Children.Add(_leftMappingButton);
            Features.UnifiedCalls.TopAppBar.TopAppBarService.SetLeftContent(container);
        }

        private void SetupRightMappingButton()
        {
            _rightMappingButton = null;
            if (_currentShortcut == null) return;

            var config = _currentShortcut.RightButton;
            if (!config.IsEnabled) return;

            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                icon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
            }

            _rightMappingButton = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Content = icon,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _rightMappingButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(transparentColor);
            _rightMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(tertiaryColor);
            _rightMappingButton.Resources = resources;

            ToolTipService.SetToolTip(_rightMappingButton, config.Tooltip);
            _rightMappingButton.Click += OnRightMappingButtonClick;
        }

        private Microsoft.UI.Xaml.Controls.AnimatedIcon CreateAnimatedIcon(string animatedIconType)
        {
            var animatedIcon = new Microsoft.UI.Xaml.Controls.AnimatedIcon
            {
                Width = 16,
                Height = 16,
                Foreground = _topBarForegroundBrush
            };

            animatedIcon.Source = animatedIconType switch
            {
                "AnimatedAcceptVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedAcceptVisualSource(),
                "AnimatedBackVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedBackVisualSource(),
                "AnimatedChevronDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronDownSmallVisualSource(),
                "AnimatedChevronRightDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronRightDownSmallVisualSource(),
                "AnimatedChevronUpDownSmallVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronUpDownSmallVisualSource(),
                "AnimatedFindVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedFindVisualSource(),
                "AnimatedGlobalNavigationButtonVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedGlobalNavigationButtonVisualSource(),
                "AnimatedSettingsVisualSource" => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedSettingsVisualSource(),
                _ => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronDownSmallVisualSource()
            };

            Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            return animatedIcon;
        }

        private async void OnLeftMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null) return;

            var config = _currentShortcut.LeftButton;

            if (_leftMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }

            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 左侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async void OnRightMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null) return;

            var config = _currentShortcut.RightButton;

            if (_rightMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }

            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);
            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 右侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async Task SendHotkeyToWebViewAsync(VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            if (WebView?.CoreWebView2 == null || key == VirtualKey.None) return;

            try
            {
                var modifiers = new List<string>();
                if (ctrl) modifiers.Add("ctrlKey: true");
                if (shift) modifiers.Add("shiftKey: true");
                if (alt) modifiers.Add("altKey: true");

                string modifiersStr = modifiers.Count > 0 ? ", " + string.Join(", ", modifiers) : "";

                string script = $@"
                    (function() {{
                        const event = new KeyboardEvent('keydown', {{
                            key: '{GetKeyString(key)}',
                            code: '{GetKeyCode(key)}',
                            keyCode: {(int)key},
                            which: {(int)key},
                            bubbles: true,
                            cancelable: true{modifiersStr}
                        }});
                        document.dispatchEvent(event);
                        
                        const eventUp = new KeyboardEvent('keyup', {{
                            key: '{GetKeyString(key)}',
                            code: '{GetKeyCode(key)}',
                            keyCode: {(int)key},
                            which: {(int)key},
                            bubbles: true,
                            cancelable: true{modifiersStr}
                        }});
                        document.dispatchEvent(eventUp);
                        
                        return 'OK';
                    }})();
                ";

                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendHotkeyToWebViewAsync] {ex.Message}");
            }
        }

        private static string GetKeyString(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Escape",
                VirtualKey.Space => " ",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.F5 => "F5",
                _ => key.ToString()
            };
        }

        private static string GetKeyCode(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.Enter => "Enter",
                VirtualKey.Tab => "Tab",
                VirtualKey.Escape => "Escape",
                VirtualKey.Space => "Space",
                VirtualKey.Back => "Backspace",
                VirtualKey.Delete => "Delete",
                VirtualKey.F5 => "F5",
                _ => $"Key{key}"
            };
        }

        private void HandleDoubleClick()
        {
            try
            {
                var window = GetMainWindowInstance();
                if (window is DockedTools.MainWindow mainWindow)
                {
                    mainWindow.ToggleWindowState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleDoubleClick] {ex.Message}");
            }
        }

        private Window? GetMainWindowInstance()
        {
            try
            {
                if (Application.Current is App app)
                {
                    var window = app.MainWindow;
                    if (window != null) return window;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetMainWindowInstance] {ex.Message}");
            }
            return null;
        }
    }
}

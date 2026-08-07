using DockedTools.Features.UnifiedCalls.TopAppBar;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Threading.Tasks;
using Windows.System;

namespace DockedTools.Features.Pages.WebApp.Browser
{
    /// <summary>
    /// 网页浏览页面 - 键盘映射按钮UI模块
    /// 包含左右映射按钮的创建、图标设置、点击事件、快捷键发送等
    /// </summary>
    public sealed partial class WebBrowserPage
    {
        private void SetupLeftMappingButton()
        {
            if (_currentShortcut == null)
            {
                return;
            }

            var config = _currentShortcut.LeftButton;
            
            // 如果未启用，不显示按钮
            if (!config.IsEnabled)
            {
                TopAppBarService.SetLeftContent(null);
                _leftMappingButton = null;
                return;
            }

            // 根据图标类型创建图标
            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                var fontIcon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
                
                // 调试日志
                System.Diagnostics.Debug.WriteLine($"[CreateLeftButton] Glyph='{config.StaticIconGlyph}' (长度={config.StaticIconGlyph?.Length}), FontFamily={fontIcon.FontFamily.Source}");
                
                icon = fontIcon;
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

            // 设置按钮背景样式（与返回按钮一致）
            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _leftMappingButton.Background = new SolidColorBrush(transparentColor);
            _leftMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            // 设置悬停和按下状态的背景色
            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
            _leftMappingButton.Resources = resources;

            ToolTipService.SetToolTip(_leftMappingButton, config.Tooltip);
            _leftMappingButton.Click += OnLeftMappingButtonClick;

            // 创建容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            container.Children.Add(_leftMappingButton);

            TopAppBarService.SetLeftContent(container);
        }

        private void SetupRightMappingButton()
        {
            _rightMappingButton = null;

            if (_currentShortcut == null)
            {
                return;
            }

            var config = _currentShortcut.RightButton;
            
            // 如果未启用，不创建按钮
            if (!config.IsEnabled)
            {
                return;
            }

            // 根据图标类型创建图标
            UIElement icon;
            if (config.IconType == "Animated")
            {
                icon = CreateAnimatedIcon(config.AnimatedIconType);
            }
            else
            {
                var fontIcon = new FontIcon
                {
                    Glyph = config.StaticIconGlyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = _topBarForegroundBrush
                };
                
                icon = fontIcon;
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

            // 设置按钮背景样式（与返回按钮一致）
            var transparentColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTransparent"];
            _rightMappingButton.Background = new SolidColorBrush(transparentColor);
            _rightMappingButton.BackgroundSizing = BackgroundSizing.InnerBorderEdge;

            // 设置悬停和按下状态的背景色
            var resources = new ResourceDictionary();
            var secondaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorSecondary"];
            var tertiaryColor = (Windows.UI.Color)Application.Current.Resources["SubtleFillColorTertiary"];
            resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(secondaryColor);
            resources["ButtonBackgroundPressed"] = new SolidColorBrush(tertiaryColor);
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

            // 根据类型名创建对应的 Source
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
                _ => new Microsoft.UI.Xaml.Controls.AnimatedVisuals.AnimatedChevronDownSmallVisualSource() // 默认
            };

            // 设置状态为 Normal
            Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");

            return animatedIcon;
        }

        private async void OnLeftMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null)
            {
                return;
            }

            var config = _currentShortcut.LeftButton;
            
            // 播放点击动画
            if (_leftMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }
            
            // 发送快捷键到 WebView2
            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 左侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async void OnRightMappingButtonClick(object sender, RoutedEventArgs e)
        {
            if (_currentShortcut == null || WebView?.CoreWebView2 == null)
            {
                return;
            }

            var config = _currentShortcut.RightButton;
            
            // 播放点击动画
            if (_rightMappingButton?.Content is Microsoft.UI.Xaml.Controls.AnimatedIcon animatedIcon)
            {
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Pressed");
                await Task.Delay(200);
                Microsoft.UI.Xaml.Controls.AnimatedIcon.SetState(animatedIcon, "Normal");
            }
            
            // 发送快捷键到 WebView2
            await SendHotkeyToWebViewAsync(config.Key, config.Ctrl, config.Shift, config.Alt);

            System.Diagnostics.Debug.WriteLine($"[WebBrowserPage] 右侧按钮发送快捷键: {config.GetHotkeyDisplayText()}");
        }

        private async Task SendHotkeyToWebViewAsync(VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            if (WebView?.CoreWebView2 == null || key == VirtualKey.None)
            {
                return;
            }

            try
            {
                // 构建修饰键字符串
                var modifiers = new System.Collections.Generic.List<string>();
                if (ctrl) modifiers.Add("ctrlKey: true");
                if (shift) modifiers.Add("shiftKey: true");
                if (alt) modifiers.Add("altKey: true");
                
                string modifiersStr = modifiers.Count > 0 ? ", " + string.Join(", ", modifiers) : "";
                
                // 使用 JavaScript 模拟键盘事件
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
                System.Diagnostics.Debug.WriteLine($"[SendHotkeyToWebViewAsync] 已发送快捷键");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendHotkeyToWebViewAsync] 发送快捷键失败: {ex.Message}");
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
    }
}
